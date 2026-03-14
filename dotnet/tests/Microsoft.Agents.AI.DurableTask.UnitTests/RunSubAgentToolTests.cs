// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.DurableTask.BuiltInTools;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.DurableTask.UnitTests;

/// <summary>
/// Unit tests for the <see cref="RunSubAgentTool"/> class.
/// </summary>
public sealed class RunSubAgentToolTests
{
    [Fact]
    public void Constructor_CreatesToolWithDefaults()
    {
        RunSubAgentTool tool = new();

        Assert.NotNull(tool.Tool);
        Assert.Equal("run_sub_agent", tool.Tool.Name);
    }

    [Fact]
    public void Constructor_WithOptions_CreatesToolWithCustomOptions()
    {
        RunSubAgentTool tool = new(new RunSubAgentToolOptions
        {
            MaxNestingDepth = 5,
            Timeout = TimeSpan.FromMinutes(30),
        });

        Assert.NotNull(tool.Tool);
        Assert.Equal("run_sub_agent", tool.Tool.Name);
    }

    [Fact]
    public void ImplicitConversion_ReturnsAITool()
    {
        AITool aiTool = new RunSubAgentTool();

        Assert.NotNull(aiTool);
        Assert.Equal("run_sub_agent", aiTool.Name);
    }

    [Fact]
    public void ImplicitConversion_NullThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            AITool _ = (RunSubAgentTool)null!;
        });
    }

    [Fact]
    public void Tool_CanBeUsedInToolList()
    {
        RunSubAgentTool subAgentTool = new();

        // Verify the tool can be used in a list of AITool (via implicit conversion)
        List<AITool> tools = [subAgentTool];

        Assert.Single(tools);
        Assert.Equal("run_sub_agent", tools[0].Name);
    }
}
