// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.DurableTask.Planning;

namespace Microsoft.Agents.AI.DurableTask.UnitTests.Planning;

public sealed class PlanTasksContextTests
{
    [Fact]
    public void Current_IsNullByDefault()
    {
        // Arrange
        PlanTasksContext.Current = null;

        // Act & Assert
        Assert.Null(PlanTasksContext.Current);
    }

    [Fact]
    public void Current_SetAndGet_Works()
    {
        // Arrange
        var tasks = new Dictionary<string, PlanTaskRecord>();

        try
        {
            // Act
            PlanTasksContext.Current = tasks;

            // Assert
            Assert.Same(tasks, PlanTasksContext.Current);
        }
        finally
        {
            PlanTasksContext.Current = null;
        }
    }

    [Fact]
    public void Current_Mutations_ReflectedInReference()
    {
        // Arrange
        var tasks = new Dictionary<string, PlanTaskRecord>();
        PlanTasksContext.Current = tasks;

        try
        {
            // Act
            tasks["test"] = new PlanTaskRecord
            {
                TaskId = "test",
                PlanGoal = "goal",
                StepCount = 1,
                CreatedAt = DateTime.UtcNow,
            };

            // Assert
            Assert.Single(PlanTasksContext.Current!);
            Assert.Equal("test", PlanTasksContext.Current!["test"].TaskId);
        }
        finally
        {
            PlanTasksContext.Current = null;
        }
    }
}
