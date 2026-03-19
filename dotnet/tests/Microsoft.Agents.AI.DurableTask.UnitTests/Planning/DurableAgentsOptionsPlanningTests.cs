// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.DurableTask.Planning;

namespace Microsoft.Agents.AI.DurableTask.UnitTests.Planning;

public sealed class DurableAgentsOptionsPlanningTests
{
    [Fact]
    public void PlanOptions_IsNullByDefault()
    {
        // Arrange & Act
        DurableAgentsOptions options = new();

        // Assert
        Assert.Null(options.PlanOptions);
    }

    [Fact]
    public void AddPlanningSupport_SetsPlanOptions()
    {
        // Arrange
        DurableAgentsOptions options = new();

        // Act
        options.AddPlanningSupport();

        // Assert
        Assert.NotNull(options.PlanOptions);
    }

    [Fact]
    public void AddPlanningSupport_WithConfiguration_AppliesSettings()
    {
        // Arrange
        DurableAgentsOptions options = new();

        // Act
        options.AddPlanningSupport(opts => opts.MaxSteps = 50);

        // Assert
        Assert.NotNull(options.PlanOptions);
        Assert.Equal(50, options.PlanOptions.MaxSteps);
    }

    [Fact]
    public void AddPlanningSupport_ReturnsOptionsForChaining()
    {
        // Arrange
        DurableAgentsOptions options = new();

        // Act
        DurableAgentsOptions result = options.AddPlanningSupport();

        // Assert
        Assert.Same(options, result);
    }
}
