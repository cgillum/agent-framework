// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.DurableTask.BuiltInTools;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.DurableTask.UnitTests;

/// <summary>
/// Unit tests for the <see cref="SubAgentInvocationContext"/> and <see cref="SubAgentCompletionContext"/> classes.
/// </summary>
public sealed class SubAgentInvocationContextTests
{
    [Fact]
    public void InvocationContext_PropertiesAreSetCorrectly()
    {
        AgentSessionId parentId = new("parent", "session1");
        AgentSessionId childId = new("child", "session2");

        SubAgentInvocationContext context = new()
        {
            ParentSessionId = parentId,
            SubAgentSessionId = childId,
            AgentName = "researcher",
            Task = "Analyze the data",
            NestingLevel = 1,
        };

        Assert.Equal(parentId, context.ParentSessionId);
        Assert.Equal(childId, context.SubAgentSessionId);
        Assert.Equal("researcher", context.AgentName);
        Assert.Equal("Analyze the data", context.Task);
        Assert.Equal(1, context.NestingLevel);
        Assert.NotNull(context.Properties);
        Assert.Empty(context.Properties);
    }

    [Fact]
    public void InvocationContext_PropertiesBagAllowsStateStorage()
    {
        SubAgentInvocationContext context = new()
        {
            ParentSessionId = new("parent", "s1"),
            SubAgentSessionId = new("child", "s2"),
            AgentName = "test",
            Task = "test task",
            NestingLevel = 1,
        };

        context.Properties["sandboxId"] = "sandbox-123";
        context.Properties["startTime"] = DateTime.UtcNow;

        Assert.Equal("sandbox-123", context.Properties["sandboxId"]);
        Assert.IsType<DateTime>(context.Properties["startTime"]);
    }

    [Fact]
    public void CompletionContext_SuccessfulCompletion()
    {
        AgentResponse response = new(new ChatMessage(ChatRole.Assistant, "Analysis complete"));

        SubAgentCompletionContext context = new()
        {
            ParentSessionId = new("parent", "s1"),
            SubAgentSessionId = new("child", "s2"),
            AgentName = "researcher",
            Task = "Analyze the data",
            NestingLevel = 1,
            Response = response,
            Exception = null,
            TimedOut = false,
        };

        Assert.NotNull(context.Response);
        Assert.Equal("Analysis complete", context.Response.Text);
        Assert.Null(context.Exception);
        Assert.False(context.TimedOut);
    }

    [Fact]
    public void CompletionContext_TimedOut()
    {
        SubAgentCompletionContext context = new()
        {
            ParentSessionId = new("parent", "s1"),
            SubAgentSessionId = new("child", "s2"),
            AgentName = "researcher",
            Task = "Long running task",
            NestingLevel = 1,
            Response = null,
            Exception = null,
            TimedOut = true,
        };

        Assert.Null(context.Response);
        Assert.Null(context.Exception);
        Assert.True(context.TimedOut);
    }

    [Fact]
    public void CompletionContext_Failed()
    {
        Exception ex = new InvalidOperationException("Agent not found");

        SubAgentCompletionContext context = new()
        {
            ParentSessionId = new("parent", "s1"),
            SubAgentSessionId = new("child", "s2"),
            AgentName = "researcher",
            Task = "Failed task",
            NestingLevel = 1,
            Response = null,
            Exception = ex,
            TimedOut = false,
        };

        Assert.Null(context.Response);
        Assert.NotNull(context.Exception);
        Assert.IsType<InvalidOperationException>(context.Exception);
        Assert.False(context.TimedOut);
    }

    [Fact]
    public void CompletionContext_InheritsFromInvocationContext()
    {
        SubAgentCompletionContext context = new()
        {
            ParentSessionId = new("parent", "s1"),
            SubAgentSessionId = new("child", "s2"),
            AgentName = "test",
            Task = "test",
            NestingLevel = 2,
        };

        // CompletionContext inherits Properties from InvocationContext
        context.Properties["key"] = "value";
        Assert.Equal("value", context.Properties["key"]);

        // It is a SubAgentInvocationContext
        Assert.IsAssignableFrom<SubAgentInvocationContext>(context);
    }
}
