// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Base class for plan step actions. Each derived type represents a different kind
/// of work that a plan step can perform.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(InvokeToolAction), "invoke_tool")]
[JsonDerivedType(typeof(InvokeAgentAction), "invoke_agent")]
[JsonDerivedType(typeof(SleepAction), "sleep")]
[JsonDerivedType(typeof(PromptAction), "prompt")]
[JsonDerivedType(typeof(WaitForInputAction), "wait_for_input")]
public abstract class PlanStepAction
{
}

/// <summary>
/// Executes a tool registered on the agent.
/// </summary>
public class InvokeToolAction : PlanStepAction
{
    /// <summary>
    /// Gets or sets the name of the tool to invoke. Must match a tool registered on the agent
    /// (and allowed by <see cref="PlanExecutionOptions.AllowedTools"/> if set).
    /// </summary>
    [JsonPropertyName("toolName")]
    public required string ToolName { get; set; }

    /// <summary>
    /// Gets or sets the arguments to pass to the tool. Values may contain
    /// <c>{step_id}</c> references that are resolved before invocation.
    /// When a reference appears in a <see cref="JsonElement"/> value, it is
    /// structurally embedded as a JSON object (not string-escaped).
    /// </summary>
    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, JsonElement>? Arguments { get; set; }
}

/// <summary>
/// Delegates work to a sub-agent, which runs in a separate durable entity session.
/// </summary>
public class InvokeAgentAction : PlanStepAction
{
    /// <summary>
    /// Gets or sets the name of the registered agent to invoke (must be allowed by
    /// <see cref="PlanExecutionOptions.AllowedAgents"/> if set).
    /// </summary>
    [JsonPropertyName("agentName")]
    public required string AgentName { get; set; }

    /// <summary>
    /// Gets or sets the task description to send to the sub-agent. May contain
    /// <c>{step_id}</c> references that are resolved before invocation.
    /// </summary>
    [JsonPropertyName("task")]
    public required string Task { get; set; }
}

/// <summary>
/// Pauses execution for a specified duration using a durable timer.
/// </summary>
public class SleepAction : PlanStepAction
{
    /// <summary>
    /// Gets or sets the duration to sleep, as an ISO 8601 duration string (e.g., "PT1H" for 1 hour).
    /// </summary>
    [JsonPropertyName("duration")]
    public required string Duration { get; set; }
}

/// <summary>
/// The agent prompts itself — makes an LLM call with accumulated context from
/// completed steps, enabling reasoning and synthesis across step results.
/// </summary>
public class PromptAction : PlanStepAction
{
    /// <summary>
    /// Gets or sets the prompt to send to the agent. May contain <c>{step_id}</c>
    /// references that are replaced with the corresponding step results.
    /// </summary>
    [JsonPropertyName("prompt")]
    public required string Prompt { get; set; }
}

/// <summary>
/// Pauses execution and waits for external human input via a durable external event.
/// </summary>
public class WaitForInputAction : PlanStepAction
{
    /// <summary>
    /// Gets or sets a description of what input is needed from the human.
    /// This is surfaced to the caller via <see cref="PendingInput.Description"/>.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets a unique name for this input request within the plan. Used to
    /// distinguish multiple pending inputs when concurrent <c>wait_for_input</c> steps
    /// are active. Defaults to the step ID if not specified.
    /// </summary>
    [JsonPropertyName("inputName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputName { get; set; }
}
