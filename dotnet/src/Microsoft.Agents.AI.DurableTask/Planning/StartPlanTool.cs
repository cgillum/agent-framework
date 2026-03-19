// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// A built-in tool that creates and starts executing a multi-step plan as a
/// background orchestration, returning immediately with a task ID for tracking.
/// </summary>
public sealed class StartPlanTool
{
    private readonly PlanExecutionOptions _options;
    private readonly AITool _tool;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartPlanTool"/> class.
    /// </summary>
    /// <param name="options">Optional execution options for plans started by this tool.</param>
    /// <param name="availableAgentNames">Names of registered agents that can be used in invoke_agent steps.</param>
    public StartPlanTool(PlanExecutionOptions? options = null, IEnumerable<string>? availableAgentNames = null)
    {
        this._options = options ?? new PlanExecutionOptions();

        StringBuilder descBuilder = new();
        descBuilder.Append(
            "Create and start executing a multi-step plan as a background task. " +
            "The plan is a DAG of steps. Each step has an action: " +
            "invoke_tool (call a tool), invoke_agent (delegate to a sub-agent), " +
            "sleep (wait a duration), prompt (LLM self-prompt with context from prior steps), " +
            "or wait_for_input (pause for human input). " +
            "Steps can reference results of completed dependency steps using {step_id} syntax. " +
            "Returns immediately with a task ID for tracking.");

        if (availableAgentNames is not null)
        {
            List<string> agents = availableAgentNames.ToList();
            if (agents.Count > 0)
            {
                descBuilder.Append(" Available agents for invoke_agent steps: ");
                descBuilder.AppendJoin(", ", agents);
                descBuilder.Append('.');
            }
        }

        this._tool = AIFunctionFactory.Create(
            this.StartPlanAsync,
            new AIFunctionFactoryOptions
            {
                Name = "start_plan",
                Description = descBuilder.ToString(),
            });
    }

    /// <summary>
    /// Gets the <see cref="AITool"/> that can be registered on an agent.
    /// </summary>
    public AITool Tool => this._tool;

    /// <summary>
    /// Converts a <see cref="StartPlanTool"/> to an <see cref="AITool"/> for use in agent tool lists.
    /// </summary>
    /// <param name="tool">The <see cref="StartPlanTool"/> to convert.</param>
    public static implicit operator AITool(StartPlanTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool._tool;
    }

    [Description("Create and start executing a multi-step plan as a background task.")]
    private Task<string> StartPlanAsync(
        [Description("The plan to execute, with goal and steps forming a DAG")] AgentPlan plan)
    {
        // 1. Validate the plan
        List<string> errors = PlanValidator.Validate(plan, options: this._options);
        if (errors.Count > 0)
        {
            StringBuilder sb = new("Plan validation failed:");
            foreach (string error in errors)
            {
                sb.Append("\n- ").Append(error);
            }

            return Task.FromResult(sb.ToString());
        }

        // 3. Check concurrent task limit
        Dictionary<string, PlanTaskRecord> planTasks = PlanTasksContext.Current ??= [];
        if (planTasks.Count >= this._options.MaxConcurrentTasks)
        {
            return Task.FromResult(
                $"Error: Maximum concurrent task limit of {this._options.MaxConcurrentTasks} reached. " +
                "Wait for existing tasks to complete or cancel them first.");
        }

        // 4. Schedule the plan task orchestration
        DurableAgentContext agentContext = DurableAgentContext.Current;
        string taskId = $"plan-{Guid.NewGuid():N}";

        PlanTaskInput input = new()
        {
            Plan = plan,
            AgentName = agentContext.CurrentSession.SessionId.Name,
            Options = this._options,
        };

        agentContext.ScheduleNewOrchestration(
            "PlanTaskOrchestration",
            input,
            new StartOrchestrationOptions { InstanceId = taskId });

        // 5. Store the task record
        planTasks[taskId] = new PlanTaskRecord
        {
            TaskId = taskId,
            PlanGoal = plan.Goal,
            StepCount = plan.Steps.Count,
            CreatedAt = DateTime.UtcNow,
        };

        // 6. Return JSON response
        return Task.FromResult(WriteJsonResponse(taskId, plan));
    }

    private static string WriteJsonResponse(string taskId, AgentPlan plan)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("taskId", taskId);
            writer.WriteString("status", "working");
            writer.WriteString("message", $"Plan '{plan.Goal}' started with {plan.Steps.Count} steps.");
            writer.WriteNumber("pollInterval", 5000);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
