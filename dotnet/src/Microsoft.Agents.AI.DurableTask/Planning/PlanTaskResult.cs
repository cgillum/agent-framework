// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// The final result of a plan task execution.
/// </summary>
public class PlanTaskResult
{
    /// <summary>
    /// Gets or sets the overall status of the plan execution.
    /// </summary>
    [JsonPropertyName("status")]
    public required PlanTaskStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the results of individual steps, keyed by step ID.
    /// </summary>
    [JsonPropertyName("stepResults")]
    public Dictionary<string, PlanStepResult> StepResults { get; set; } = new();

    /// <summary>
    /// Gets or sets an error message if the plan execution failed.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static PlanTaskResult Completed(Dictionary<string, PlanStepResult> stepResults)
        => new() { Status = PlanTaskStatus.Completed, StepResults = stepResults };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static PlanTaskResult Failed(string stepId, string error)
        => new() { Status = PlanTaskStatus.Failed, Error = $"Step '{stepId}' failed: {error}" };
}

/// <summary>
/// The overall status of a plan task.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlanTaskStatus>))]
public enum PlanTaskStatus
{
    /// <summary>The plan task is currently executing steps.</summary>
    Working,

    /// <summary>The plan task is waiting for human input on one or more steps.</summary>
    InputRequired,

    /// <summary>The plan task is re-planning after a step failure.</summary>
    Replanning,

    /// <summary>All steps completed successfully.</summary>
    Completed,

    /// <summary>The plan task failed and could not recover.</summary>
    Failed,

    /// <summary>The plan task was cancelled.</summary>
    Cancelled,
}
