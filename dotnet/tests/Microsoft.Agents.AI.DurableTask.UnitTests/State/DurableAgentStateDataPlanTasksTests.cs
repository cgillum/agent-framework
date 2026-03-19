// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.Agents.AI.DurableTask.Planning;
using Microsoft.Agents.AI.DurableTask.State;

namespace Microsoft.Agents.AI.DurableTask.Tests.Unit.State;

public sealed class DurableAgentStateDataPlanTasksTests
{
    [Fact]
    public void Deserialize_OldStateWithoutPlanTasks_PlanTasksIsNull()
    {
        // Arrange - JSON that matches the old schema (no planTasks field)
        const string JsonText = """
            {
                "schemaVersion": "1.0.0",
                "data": {
                    "conversationHistory": [],
                    "expirationTimeUtc": null
                }
            }
            """;

        // Act
        DurableAgentState? state = JsonSerializer.Deserialize(
            JsonText,
            DurableAgentStateJsonContext.Default.DurableAgentState);

        // Assert
        Assert.NotNull(state);
        Assert.Null(state.Data.PlanTasks);
    }

    [Fact]
    public void PlanTasks_RoundTrip_SerializesCorrectly()
    {
        // Arrange
        DurableAgentState state = new()
        {
            Data = new DurableAgentStateData
            {
                ConversationHistory = [],
                PlanTasks = new Dictionary<string, PlanTaskRecord>
                {
                    ["task-1"] = new PlanTaskRecord
                    {
                        TaskId = "task-1",
                        PlanGoal = "Research competitors",
                        StepCount = 5,
                        CreatedAt = new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc),
                    },
                },
            },
        };

        // Act
        string json = JsonSerializer.Serialize(state, DurableAgentStateJsonContext.Default.DurableAgentState);
        DurableAgentState? deserialized = JsonSerializer.Deserialize(json, DurableAgentStateJsonContext.Default.DurableAgentState);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Data.PlanTasks);
        Assert.Single(deserialized.Data.PlanTasks);
        Assert.True(deserialized.Data.PlanTasks.ContainsKey("task-1"));

        PlanTaskRecord record = deserialized.Data.PlanTasks["task-1"];
        Assert.Equal("task-1", record.TaskId);
        Assert.Equal("Research competitors", record.PlanGoal);
        Assert.Equal(5, record.StepCount);
        Assert.Equal(new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc), record.CreatedAt);
    }

    [Fact]
    public void PlanTasks_WhenNull_OmittedFromJson()
    {
        // Arrange
        DurableAgentState state = new()
        {
            Data = new DurableAgentStateData
            {
                ConversationHistory = [],
                PlanTasks = null,
            },
        };

        // Act
        string json = JsonSerializer.Serialize(state, DurableAgentStateJsonContext.Default.DurableAgentState);

        // Assert
        Assert.DoesNotContain("planTasks", json);
    }

    [Fact]
    public void PlanTasks_WhenEmptyDictionary_SerializesCorrectly()
    {
        // Arrange
        DurableAgentState state = new()
        {
            Data = new DurableAgentStateData
            {
                ConversationHistory = [],
                PlanTasks = new Dictionary<string, PlanTaskRecord>(),
            },
        };

        // Act
        string json = JsonSerializer.Serialize(state, DurableAgentStateJsonContext.Default.DurableAgentState);
        DurableAgentState? deserialized = JsonSerializer.Deserialize(json, DurableAgentStateJsonContext.Default.DurableAgentState);

        // Assert - empty dict is not null, so it should be serialized (JsonIgnoreCondition.WhenWritingNull)
        Assert.Contains("planTasks", json);
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Data.PlanTasks);
        Assert.Empty(deserialized.Data.PlanTasks);
    }
}
