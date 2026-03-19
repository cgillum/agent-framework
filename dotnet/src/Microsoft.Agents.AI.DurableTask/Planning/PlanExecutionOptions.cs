// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;
using Microsoft.DurableTask;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Configuration options for plan task execution.
/// </summary>
public class PlanExecutionOptions
{
    /// <summary>
    /// Gets or sets whether the agent can revise the plan if a step fails.
    /// </summary>
    [JsonPropertyName("enableReplanning")]
    public bool EnableReplanning { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of re-planning attempts.
    /// </summary>
    [JsonPropertyName("maxReplanAttempts")]
    public int MaxReplanAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of steps allowed in a plan.
    /// </summary>
    [JsonPropertyName("maxSteps")]
    public int MaxSteps { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum nesting depth for <c>invoke_agent</c> steps.
    /// </summary>
    [JsonPropertyName("maxAgentNestingDepth")]
    public int MaxAgentNestingDepth { get; set; } = 2;

    /// <summary>
    /// Gets or sets the maximum number of concurrent plan tasks per agent session.
    /// </summary>
    [JsonPropertyName("maxConcurrentTasks")]
    public int MaxConcurrentTasks { get; set; } = 5;

    /// <summary>
    /// Gets or sets the timeout for <c>wait_for_input</c> steps.
    /// </summary>
    [JsonPropertyName("waitForInputTimeout")]
    public TimeSpan WaitForInputTimeout { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the timeout for <c>invoke_tool</c> steps.
    /// </summary>
    [JsonPropertyName("toolInvocationTimeout")]
    public TimeSpan ToolInvocationTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets the timeout for <c>invoke_agent</c> steps.
    /// </summary>
    [JsonPropertyName("agentInvocationTimeout")]
    public TimeSpan AgentInvocationTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the retry policy for <c>invoke_tool</c> steps. Applies when a
    /// tool throws an exception (not when it returns an error result). Error results
    /// flow to re-planning instead. <c>null</c> means no retries (fail immediately).
    /// </summary>
    [JsonIgnore]
    public TaskOptions? ToolRetryPolicy { get; set; }

    /// <summary>
    /// Gets or sets the allowlist of tool names permitted in <c>invoke_tool</c> steps.
    /// <c>null</c> (default) means all registered tools are allowed.
    /// </summary>
    [JsonIgnore]
    public ISet<string>? AllowedTools { get; set; }

    /// <summary>
    /// Gets or sets the allowlist of agent names permitted in <c>invoke_agent</c> steps.
    /// <c>null</c> (default) means all registered agents are allowed.
    /// </summary>
    [JsonIgnore]
    public ISet<string>? AllowedAgents { get; set; }
}
