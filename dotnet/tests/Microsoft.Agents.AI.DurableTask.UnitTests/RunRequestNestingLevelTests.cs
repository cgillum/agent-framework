// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;

namespace Microsoft.Agents.AI.DurableTask.UnitTests;

/// <summary>
/// Unit tests for the nesting level tracking in <see cref="RunRequest"/>.
/// </summary>
public sealed class RunRequestNestingLevelTests
{
    [Fact]
    public void NestingLevel_DefaultIsZero()
    {
        RunRequest request = new("Hello");

        Assert.Equal(0, request.NestingLevel);
    }

    [Fact]
    public void NestingLevel_CanBeSetViaInitializer()
    {
        RunRequest request = new("Hello")
        {
            NestingLevel = 3,
        };

        Assert.Equal(3, request.NestingLevel);
    }

    [Fact]
    public void NestingLevel_RoundTripsViaSerialization()
    {
        RunRequest original = new("Test message")
        {
            NestingLevel = 2,
            OrchestrationId = "orch-123",
        };

        string json = JsonSerializer.Serialize(original);
        RunRequest? deserialized = JsonSerializer.Deserialize<RunRequest>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.NestingLevel);
    }

    [Fact]
    public void NestingLevel_OmittedFromJsonWhenDefault()
    {
        RunRequest request = new("Test message");

        string json = JsonSerializer.Serialize(request);

        // NestingLevel should be omitted when it's 0 (default) due to JsonIgnoreCondition.WhenWritingDefault
        Assert.DoesNotContain("nestingLevel", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NestingLevel_IncludedInJsonWhenNonDefault()
    {
        RunRequest request = new("Test message")
        {
            NestingLevel = 1,
        };

        string json = JsonSerializer.Serialize(request);

        // NestingLevel should be included when non-zero
        Assert.Contains("NestingLevel", json);
    }
}
