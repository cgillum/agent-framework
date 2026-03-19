// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.Agents.AI.DurableTask.Planning;

namespace Microsoft.Agents.AI.DurableTask.UnitTests.Planning;

public sealed class PlanTaskRecordTests
{
    [Fact]
    public void PlanTaskRecord_RoundTrip_SerializesCorrectly()
    {
        // Arrange
        var record = new PlanTaskRecord
        {
            TaskId = "task-123",
            PlanGoal = "Research competitors",
            StepCount = 5,
            CreatedAt = new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc),
        };

        // Act
        string json = JsonSerializer.Serialize(record, DurableAgentJsonUtilities.DefaultOptions);
        var deserialized = JsonSerializer.Deserialize<PlanTaskRecord>(json, DurableAgentJsonUtilities.DefaultOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(record.TaskId, deserialized.TaskId);
        Assert.Equal(record.PlanGoal, deserialized.PlanGoal);
        Assert.Equal(record.StepCount, deserialized.StepCount);
        Assert.Equal(record.CreatedAt, deserialized.CreatedAt);
    }
}
