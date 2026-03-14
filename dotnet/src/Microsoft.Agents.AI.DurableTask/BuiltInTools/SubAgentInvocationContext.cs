// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Agents.AI.DurableTask.BuiltInTools;

/// <summary>
/// Provides context information about a sub-agent invocation. This context is passed to
/// the <see cref="RunSubAgentToolOptions.OnBeforeSubAgentRun"/> and
/// <see cref="RunSubAgentToolOptions.OnAfterSubAgentRun"/> lifecycle hooks.
/// </summary>
public class SubAgentInvocationContext
{
    /// <summary>
    /// Gets the session ID of the parent agent that is creating the sub-agent.
    /// </summary>
    public required AgentSessionId ParentSessionId { get; init; }

    /// <summary>
    /// Gets the session ID assigned to the sub-agent.
    /// </summary>
    public required AgentSessionId SubAgentSessionId { get; init; }

    /// <summary>
    /// Gets the name of the agent type being used for the sub-agent.
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// Gets the task description that was delegated to the sub-agent.
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// Gets the nesting level of the sub-agent. A value of 1 indicates a direct child of the root agent.
    /// </summary>
    public required int NestingLevel { get; init; }

    /// <summary>
    /// Gets a property bag that can be used to pass state between the
    /// <see cref="RunSubAgentToolOptions.OnBeforeSubAgentRun"/> and
    /// <see cref="RunSubAgentToolOptions.OnAfterSubAgentRun"/> hooks.
    /// For example, a sandbox ID created in the before hook can be stored here
    /// and retrieved in the after hook for cleanup.
    /// </summary>
    public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
}

/// <summary>
/// Provides context information about a completed sub-agent invocation. This context is passed
/// to the <see cref="RunSubAgentToolOptions.OnAfterSubAgentRun"/> lifecycle hook.
/// </summary>
public class SubAgentCompletionContext : SubAgentInvocationContext
{
    /// <summary>
    /// Gets the response from the sub-agent, or <see langword="null"/> if the sub-agent
    /// timed out or failed before producing a response.
    /// </summary>
    public AgentResponse? Response { get; init; }

    /// <summary>
    /// Gets the exception that occurred during sub-agent execution, or <see langword="null"/>
    /// if the sub-agent completed successfully.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets a value indicating whether the sub-agent invocation timed out.
    /// </summary>
    public bool TimedOut { get; init; }
}
