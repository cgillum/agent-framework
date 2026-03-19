// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Progress information published by the plan task orchestration via
/// <c>SetCustomStatus</c>, readable by <c>get_task_status</c> tool.
/// </summary>
public class TaskProgress
{
    /// <summary>
    /// Gets or sets the current status of the plan task.
    /// </summary>
    [JsonPropertyName("status")]
    public required PlanTaskStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a human-readable status message.
    /// </summary>
    [JsonPropertyName("statusMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Gets or sets the IDs of steps that have completed.
    /// </summary>
    [JsonPropertyName("completedSteps")]
    public List<string> CompletedSteps { get; set; } = [];

    /// <summary>
    /// Gets or sets the IDs of steps currently executing.
    /// </summary>
    [JsonPropertyName("currentSteps")]
    public List<string> CurrentSteps { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of pending human input requests. Non-null when
    /// <see cref="Status"/> is <see cref="PlanTaskStatus.InputRequired"/>.
    /// </summary>
    [JsonPropertyName("pendingInputs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PendingInput>? PendingInputs { get; set; }
}

/// <summary>
/// Describes a pending human input request from a <c>wait_for_input</c> step.
/// </summary>
public class PendingInput
{
    /// <summary>
    /// Gets or sets the ID of the step waiting for input.
    /// </summary>
    [JsonPropertyName("stepId")]
    public required string StepId { get; set; }

    /// <summary>
    /// Gets or sets the input name used by <c>send_task_input</c> to route input
    /// to the correct step. Defaults to the step ID.
    /// </summary>
    [JsonPropertyName("inputName")]
    public required string InputName { get; set; }

    /// <summary>
    /// Gets or sets a description of what input is needed from the human.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }
}
