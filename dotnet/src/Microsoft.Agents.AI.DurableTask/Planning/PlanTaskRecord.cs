// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// A record of a plan task stored in entity state. Used to track active and
/// completed tasks independently of conversation history, ensuring task IDs
/// survive LLM context compaction.
/// </summary>
public class PlanTaskRecord
{
    /// <summary>
    /// Gets or sets the orchestration instance ID for this task.
    /// </summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; set; }

    /// <summary>
    /// Gets or sets the goal from the plan that this task is executing.
    /// </summary>
    [JsonPropertyName("planGoal")]
    public required string PlanGoal { get; set; }

    /// <summary>
    /// Gets or sets the total number of steps in the plan.
    /// </summary>
    [JsonPropertyName("stepCount")]
    public required int StepCount { get; set; }

    /// <summary>
    /// Gets or sets the time when this task was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; set; }
}
