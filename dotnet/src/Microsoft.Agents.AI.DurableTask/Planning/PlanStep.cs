// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Represents a single node in a plan DAG.
/// </summary>
public class PlanStep
{
    /// <summary>
    /// Gets or sets the unique identifier for this step, referenced by other steps'
    /// <see cref="DependsOn"/> lists and by <c>{step_id}</c> template references in
    /// action fields.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of what this step does.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the action to perform when this step executes.
    /// </summary>
    [JsonPropertyName("action")]
    public required PlanStepAction Action { get; set; }

    /// <summary>
    /// Gets or sets the IDs of steps that must complete before this step can execute.
    /// An empty or null list means this step has no dependencies and can run immediately.
    /// </summary>
    [JsonPropertyName("dependsOn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? DependsOn { get; set; }
}
