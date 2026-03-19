// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Options for generating a plan from a natural-language request using an LLM.
/// </summary>
public class PlanGenerationOptions
{
    /// <summary>
    /// Gets or sets an optional system prompt override for the plan generation LLM call.
    /// When <see langword="null"/>, a default system prompt is used.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Gets or sets additional instructions appended to the system prompt.
    /// </summary>
    public string? AdditionalInstructions { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries when the LLM produces a plan
    /// that fails validation.
    /// </summary>
    public int MaxValidationRetries { get; set; } = 2;
}
