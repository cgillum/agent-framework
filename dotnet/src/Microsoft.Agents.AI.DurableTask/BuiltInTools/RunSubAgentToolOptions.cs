// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Agents.AI.DurableTask.BuiltInTools;

/// <summary>
/// Configuration options for the <see cref="RunSubAgentTool"/>.
/// </summary>
public sealed class RunSubAgentToolOptions
{
    /// <summary>
    /// Gets or sets the maximum allowed nesting depth for sub-agents.
    /// A value of 0 means no sub-agents can be created.
    /// A value of 2 (the default) means root → child → grandchild.
    /// </summary>
    public int MaxNestingDepth { get; set; } = 2;

    /// <summary>
    /// Gets or sets the maximum time to wait for a sub-agent to complete before timing out.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets an optional callback that is invoked before the sub-agent is scheduled to run.
    /// Use this to set up resources such as sandboxes, permissions, or other prerequisites.
    /// </summary>
    /// <remarks>
    /// If this callback is set and invoked, <see cref="OnAfterSubAgentRun"/> is guaranteed to be
    /// called afterward (in a <c>finally</c> block), even if the sub-agent fails or times out.
    /// State can be passed between the before and after hooks via the
    /// <see cref="SubAgentInvocationContext.Properties"/> dictionary.
    /// </remarks>
    public Func<SubAgentInvocationContext, CancellationToken, Task>? OnBeforeSubAgentRun { get; set; }

    /// <summary>
    /// Gets or sets an optional callback that is invoked after the sub-agent completes, times out, or fails.
    /// Use this to tear down resources such as sandboxes or perform cleanup.
    /// </summary>
    /// <remarks>
    /// This callback is guaranteed to be called if <see cref="OnBeforeSubAgentRun"/> was called,
    /// even if the sub-agent fails or times out. The <see cref="SubAgentCompletionContext"/> provides
    /// information about the outcome, including any exception or timeout status.
    /// </remarks>
    public Func<SubAgentCompletionContext, CancellationToken, Task>? OnAfterSubAgentRun { get; set; }
}
