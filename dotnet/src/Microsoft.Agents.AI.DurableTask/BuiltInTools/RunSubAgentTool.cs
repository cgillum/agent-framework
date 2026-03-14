// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.AI.DurableTask.BuiltInTools;

/// <summary>
/// A built-in tool that allows a durable agent to delegate a task to a sub-agent.
/// The sub-agent runs as a separate durable entity and its result is returned as the tool output.
/// </summary>
public sealed class RunSubAgentTool
{
    private readonly RunSubAgentToolOptions _options;
    private readonly AITool _tool;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunSubAgentTool"/> class.
    /// </summary>
    /// <param name="options">Optional configuration for the sub-agent tool.</param>
    public RunSubAgentTool(RunSubAgentToolOptions? options = null)
    {
        this._options = options ?? new RunSubAgentToolOptions();
        this._tool = AIFunctionFactory.Create(
            this.RunSubAgentAsync,
            new AIFunctionFactoryOptions
            {
                Name = "run_sub_agent",
                Description =
                    "Delegate a task to a sub-agent. The sub-agent runs autonomously and returns its result. " +
                    "Use this when a task would benefit from independent, focused execution. " +
                    "The sub-agent does not have access to this conversation's history, so include all " +
                    "necessary context in the task description.",
            });
    }

    /// <summary>
    /// Gets the <see cref="AITool"/> that can be registered on an agent.
    /// </summary>
    public AITool Tool => this._tool;

    /// <summary>
    /// Converts a <see cref="RunSubAgentTool"/> to an <see cref="AITool"/> for use in agent tool lists.
    /// </summary>
    /// <param name="tool">The <see cref="RunSubAgentTool"/> to convert.</param>
    public static implicit operator AITool(RunSubAgentTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool._tool;
    }

    [Description("Delegate a task to a sub-agent.")]
    private async Task<string> RunSubAgentAsync(
        [Description("A clear description of the task to delegate. Include all necessary context.")] string task,
        [Description("The name of the registered agent to use. If omitted, uses the same agent type as the current agent.")] string? agentName = null,
        CancellationToken cancellationToken = default)
    {
        // Access the current durable agent context (set by AgentEntity during execution)
        DurableAgentContext agentContext = DurableAgentContext.Current;
        int currentNestingLevel = agentContext.NestingLevel;
        int childNestingLevel = currentNestingLevel + 1;

        // Validate nesting depth
        if (childNestingLevel > this._options.MaxNestingDepth)
        {
            return $"Error: Maximum sub-agent nesting depth of {this._options.MaxNestingDepth} exceeded. " +
                   $"Current depth is {currentNestingLevel}. Cannot create additional sub-agents.";
        }

        // Resolve agent name: default to current agent's type
        AgentSessionId parentSessionId = agentContext.CurrentSession.SessionId;
        agentName ??= parentSessionId.Name;

        // Create a unique session ID for the sub-agent
        AgentSessionId subAgentSessionId = AgentSessionId.WithRandomKey(agentName);

        // Build the run request for the sub-agent
        RunRequest request = new(task)
        {
            NestingLevel = childNestingLevel,
        };

        // Set up invocation context for lifecycle hooks
        SubAgentInvocationContext invocationContext = new()
        {
            ParentSessionId = parentSessionId,
            SubAgentSessionId = subAgentSessionId,
            AgentName = agentName,
            Task = task,
            NestingLevel = childNestingLevel,
        };

        ILoggerFactory loggerFactory = agentContext.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        ILogger logger = loggerFactory.CreateLogger<RunSubAgentTool>();

        logger.LogSubAgentCreating(parentSessionId, subAgentSessionId, agentName, childNestingLevel);

        // Call the before hook if configured
        bool beforeHookCalled = false;
        if (this._options.OnBeforeSubAgentRun is not null)
        {
            await this._options.OnBeforeSubAgentRun(invocationContext, cancellationToken);
            beforeHookCalled = true;
        }

        AgentResponse? response = null;
        Exception? exception = null;
        bool timedOut = false;

        try
        {
            // Create a client to signal the sub-agent entity
            DurableTaskClient client = agentContext.Client;
            DefaultDurableAgentClient durableAgentClient = new(client, loggerFactory);

            // Signal the sub-agent entity and get a polling handle
            AgentRunHandle handle = await durableAgentClient.RunAgentAsync(
                subAgentSessionId,
                request,
                cancellationToken);

            // Poll for the response with timeout
            using CancellationTokenSource timeoutCts = new(this._options.Timeout);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token);

            try
            {
                response = await handle.ReadAgentResponseAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                logger.LogSubAgentTimedOut(parentSessionId, subAgentSessionId, this._options.Timeout);
                return $"Error: Sub-agent '{agentName}' timed out after {this._options.Timeout.TotalMinutes:F0} minutes. " +
                       "The sub-agent may still be running but its result will not be available.";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            exception = ex;
            logger.LogSubAgentFailed(parentSessionId, subAgentSessionId, ex);
            return $"Error: Sub-agent '{agentName}' failed with error: {ex.Message}";
        }
        finally
        {
            // Guaranteed cleanup: call the after hook if the before hook was called
            if (beforeHookCalled && this._options.OnAfterSubAgentRun is not null)
            {
                SubAgentCompletionContext completionContext = new()
                {
                    ParentSessionId = invocationContext.ParentSessionId,
                    SubAgentSessionId = invocationContext.SubAgentSessionId,
                    AgentName = invocationContext.AgentName,
                    Task = invocationContext.Task,
                    NestingLevel = invocationContext.NestingLevel,
                    Response = response,
                    Exception = exception,
                    TimedOut = timedOut,
                };

                // Copy properties from invocation context so the after hook can access state set by the before hook
                foreach (KeyValuePair<string, object?> kvp in invocationContext.Properties)
                {
                    completionContext.Properties[kvp.Key] = kvp.Value;
                }

                await this._options.OnAfterSubAgentRun(completionContext, CancellationToken.None);
            }
        }

        logger.LogSubAgentCompleted(parentSessionId, subAgentSessionId);

        return response?.Text ?? "(No response from sub-agent)";
    }
}
