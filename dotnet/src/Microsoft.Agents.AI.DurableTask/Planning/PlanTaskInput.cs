// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Input for the plan task execution orchestration.
/// </summary>
public class PlanTaskInput
{
    /// <summary>
    /// Gets or sets the plan to execute.
    /// </summary>
    [JsonPropertyName("plan")]
    public required AgentPlan Plan { get; set; }

    /// <summary>
    /// Gets or sets the name of the agent that created this plan.
    /// Used for <c>prompt</c> and re-planning steps that call back to the agent.
    /// </summary>
    [JsonPropertyName("agentName")]
    public required string AgentName { get; set; }

    /// <summary>
    /// Gets or sets the execution options for this plan task.
    /// </summary>
    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PlanExecutionOptions? Options { get; set; }
}
