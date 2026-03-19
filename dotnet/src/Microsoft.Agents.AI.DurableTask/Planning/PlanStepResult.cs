// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// The result of executing a single step in a plan.
/// </summary>
public class PlanStepResult
{
    /// <summary>
    /// Gets or sets the ID of the step that produced this result.
    /// </summary>
    [JsonPropertyName("stepId")]
    public required string StepId { get; set; }

    /// <summary>
    /// Gets or sets the status of the step execution.
    /// </summary>
    [JsonPropertyName("status")]
    public required PlanStepStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the text result of the step, used for <c>{step_id}</c> template
    /// resolution in string fields of dependent steps.
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Result { get; set; }

    /// <summary>
    /// Gets or sets the structured JSON result of the step, used for <c>{step_id}</c>
    /// template resolution in <see cref="JsonElement"/> fields of dependent steps.
    /// When referenced, the JSON value is structurally embedded (not string-escaped).
    /// </summary>
    [JsonPropertyName("jsonResult")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? JsonResult { get; set; }

    /// <summary>
    /// Gets or sets the error message if the step failed.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the time when this step completed.
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; set; }
}

/// <summary>
/// The status of an individual plan step.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlanStepStatus>))]
public enum PlanStepStatus
{
    /// <summary>The step completed successfully.</summary>
    Completed,

    /// <summary>The step failed.</summary>
    Failed,

    /// <summary>The step was skipped (e.g., due to re-planning).</summary>
    Skipped,
}
