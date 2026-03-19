// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Represents a plan — a directed acyclic graph (DAG) of steps that an agent
/// generates for executing a complex, multi-step task.
/// </summary>
/// <remarks>
/// A plan is a document that describes <em>what</em> to do. It is distinct from a
/// <em>task</em>, which is the long-running, durable execution of a plan.
/// </remarks>
public class AgentPlan
{
    /// <summary>
    /// Gets or sets a high-level description of what this plan accomplishes.
    /// </summary>
    [JsonPropertyName("goal")]
    public required string Goal { get; set; }

    /// <summary>
    /// Gets or sets the steps in the plan. The steps form a DAG via
    /// <see cref="PlanStep.DependsOn"/> references.
    /// </summary>
    [JsonPropertyName("steps")]
    public required List<PlanStep> Steps { get; set; }
}
