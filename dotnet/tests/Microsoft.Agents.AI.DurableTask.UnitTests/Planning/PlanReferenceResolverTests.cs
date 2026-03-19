// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.Agents.AI.DurableTask.Planning;

namespace Microsoft.Agents.AI.DurableTask.UnitTests.Planning;

public sealed class PlanReferenceResolverTests
{
    [Fact]
    public void StringReference_ResolvedCorrectly()
    {
        Dictionary<string, PlanStepResult> results = new()
        {
            ["fetch"] = new PlanStepResult
            {
                StepId = "fetch",
                Status = PlanStepStatus.Completed,
                Result = "Hello World",
            },
        };

        string resolved = PlanReferenceResolver.ResolveString(
            "The result is: {fetch}", results);

        Assert.Equal("The result is: Hello World", resolved);
    }

    [Fact]
    public void JsonExactReference_EmbedsAsJsonObject()
    {
        JsonElement jsonResult = JsonDocument.Parse("{\"name\":\"Alice\",\"age\":30}").RootElement.Clone();

        Dictionary<string, PlanStepResult> results = new()
        {
            ["lookup"] = new PlanStepResult
            {
                StepId = "lookup",
                Status = PlanStepStatus.Completed,
                Result = "{\"name\":\"Alice\",\"age\":30}",
                JsonResult = jsonResult,
            },
        };

        // Exact reference: the entire value is "{lookup}"
        InvokeToolAction action = new()
        {
            ToolName = "process",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["data"] = CreateJsonString("{lookup}"),
            },
        };

        PlanStepAction resolved = PlanReferenceResolver.ResolveReferences(action, results);
        InvokeToolAction resolvedTool = Assert.IsType<InvokeToolAction>(resolved);

        Assert.NotNull(resolvedTool.Arguments);
        JsonElement resolvedData = resolvedTool.Arguments["data"];

        // Should be structurally embedded as a JSON object, not a string
        Assert.Equal(JsonValueKind.Object, resolvedData.ValueKind);
        Assert.Equal("Alice", resolvedData.GetProperty("name").GetString());
        Assert.Equal(30, resolvedData.GetProperty("age").GetInt32());
    }

    [Fact]
    public void JsonPartialReference_InString_ResolvesAsText()
    {
        Dictionary<string, PlanStepResult> results = new()
        {
            ["step1"] = new PlanStepResult
            {
                StepId = "step1",
                Status = PlanStepStatus.Completed,
                Result = "42",
            },
        };

        InvokeToolAction action = new()
        {
            ToolName = "calc",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["query"] = CreateJsonString("The answer is {step1}"),
            },
        };

        PlanStepAction resolved = PlanReferenceResolver.ResolveReferences(action, results);
        InvokeToolAction resolvedTool = Assert.IsType<InvokeToolAction>(resolved);

        Assert.NotNull(resolvedTool.Arguments);
        JsonElement resolvedQuery = resolvedTool.Arguments["query"];
        Assert.Equal(JsonValueKind.String, resolvedQuery.ValueKind);
        Assert.Equal("The answer is 42", resolvedQuery.GetString());
    }

    [Fact]
    public void UnresolvedReference_LeftAsIs()
    {
        Dictionary<string, PlanStepResult> results = new();

        string resolved = PlanReferenceResolver.ResolveString(
            "Missing: {nonexistent}", results);

        Assert.Equal("Missing: {nonexistent}", resolved);
    }

    [Fact]
    public void NestedJsonObject_ReferencesResolved()
    {
        Dictionary<string, PlanStepResult> results = new()
        {
            ["data"] = new PlanStepResult
            {
                StepId = "data",
                Status = PlanStepStatus.Completed,
                Result = "resolved_value",
            },
        };

        // Build a nested JSON object: { "outer": { "inner": "{data}" } }
        JsonElement nestedJson = JsonDocument.Parse(
            "{\"outer\":{\"inner\":\"{data}\"}}").RootElement.Clone();

        InvokeToolAction action = new()
        {
            ToolName = "process",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["config"] = nestedJson,
            },
        };

        PlanStepAction resolved = PlanReferenceResolver.ResolveReferences(action, results);
        InvokeToolAction resolvedTool = Assert.IsType<InvokeToolAction>(resolved);

        Assert.NotNull(resolvedTool.Arguments);
        JsonElement config = resolvedTool.Arguments["config"];
        string? innerValue = config.GetProperty("outer").GetProperty("inner").GetString();
        Assert.Equal("resolved_value", innerValue);
    }

    [Fact]
    public void PromptAction_ReferencesResolved()
    {
        Dictionary<string, PlanStepResult> results = new()
        {
            ["research"] = new PlanStepResult
            {
                StepId = "research",
                Status = PlanStepStatus.Completed,
                Result = "quantum computing advances",
            },
        };

        PromptAction action = new() { Prompt = "Summarize: {research}" };
        PlanStepAction resolved = PlanReferenceResolver.ResolveReferences(action, results);
        PromptAction resolvedPrompt = Assert.IsType<PromptAction>(resolved);

        Assert.Equal("Summarize: quantum computing advances", resolvedPrompt.Prompt);
    }

    [Fact]
    public void InvokeAgentAction_TaskReferencesResolved()
    {
        Dictionary<string, PlanStepResult> results = new()
        {
            ["gather"] = new PlanStepResult
            {
                StepId = "gather",
                Status = PlanStepStatus.Completed,
                Result = "customer feedback data",
            },
        };

        InvokeAgentAction action = new()
        {
            AgentName = "analyzer",
            Task = "Analyze the following data: {gather}",
        };

        PlanStepAction resolved = PlanReferenceResolver.ResolveReferences(action, results);
        InvokeAgentAction resolvedAgent = Assert.IsType<InvokeAgentAction>(resolved);

        Assert.Equal("analyzer", resolvedAgent.AgentName);
        Assert.Equal("Analyze the following data: customer feedback data", resolvedAgent.Task);
    }

    private static JsonElement CreateJsonString(string value)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStringValue(value);
        }

        using var doc = JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.Clone();
    }
}
