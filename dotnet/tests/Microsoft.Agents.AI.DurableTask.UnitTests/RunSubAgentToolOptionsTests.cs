// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.DurableTask.BuiltInTools;

namespace Microsoft.Agents.AI.DurableTask.UnitTests;

/// <summary>
/// Unit tests for the <see cref="RunSubAgentToolOptions"/> class.
/// </summary>
public sealed class RunSubAgentToolOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveExpectedValues()
    {
        RunSubAgentToolOptions options = new();

        Assert.Equal(2, options.MaxNestingDepth);
        Assert.Equal(TimeSpan.FromMinutes(10), options.Timeout);
        Assert.Null(options.OnBeforeSubAgentRun);
        Assert.Null(options.OnAfterSubAgentRun);
    }

    [Fact]
    public void Options_CanBeCustomized()
    {
        RunSubAgentToolOptions options = new()
        {
            MaxNestingDepth = 5,
            Timeout = TimeSpan.FromMinutes(30),
            OnBeforeSubAgentRun = (ctx, ct) => Task.CompletedTask,
            OnAfterSubAgentRun = (ctx, ct) => Task.CompletedTask,
        };

        Assert.Equal(5, options.MaxNestingDepth);
        Assert.Equal(TimeSpan.FromMinutes(30), options.Timeout);
        Assert.NotNull(options.OnBeforeSubAgentRun);
        Assert.NotNull(options.OnAfterSubAgentRun);
    }
}
