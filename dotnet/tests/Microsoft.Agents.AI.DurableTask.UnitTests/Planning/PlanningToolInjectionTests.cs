// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.DurableTask.Planning;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.DurableTask.UnitTests.Planning;

public sealed class PlanningToolInjectionTests
{
    [Fact]
    public void PlanningTools_CanBeInjectedPostConstruction()
    {
        // Arrange — create an agent with instructions but no tools (matching sample pattern)
        var stubClient = new StubChatClient();
        var agent = new ChatClientAgent(stubClient, "test instructions", "TestAgent");

        // Act — inject planning tools the same way ConfigureDurableAgents does
        ChatClientAgentOptions? agentOptions = agent.GetService<ChatClientAgentOptions>();
        Assert.NotNull(agentOptions);
        Assert.NotNull(agentOptions.ChatOptions);

        agentOptions.ChatOptions.Tools ??= [];
        agentOptions.ChatOptions.Tools.Add(new StartPlanTool().Tool);
        agentOptions.ChatOptions.Tools.Add(new ListTasksTool().Tool);
        agentOptions.ChatOptions.Tools.Add(new GetTaskStatusTool().Tool);
        agentOptions.ChatOptions.Tools.Add(new SendTaskInputTool().Tool);
        agentOptions.ChatOptions.Tools.Add(new CancelTaskTool().Tool);

        // Assert — tools are visible via the agent's internal options
        ChatClientAgentOptions? verified = agent.GetService<ChatClientAgentOptions>();
        Assert.NotNull(verified?.ChatOptions?.Tools);
        Assert.Equal(5, verified.ChatOptions.Tools.Count);

        HashSet<string> toolNames = new(verified.ChatOptions.Tools.Select(t => t.Name));
        Assert.Contains("start_plan", toolNames);
        Assert.Contains("list_tasks", toolNames);
        Assert.Contains("get_task_status", toolNames);
        Assert.Contains("send_task_input", toolNames);
        Assert.Contains("cancel_task", toolNames);
    }

    [Fact]
    public void PlanningTools_InjectedViaGetService_PersistAcrossCalls()
    {
        // Verify the tools are on the same ChatOptions reference (not cloned away)
        var stubClient = new StubChatClient();
        var agent = new ChatClientAgent(stubClient, "test instructions", "TestAgent");

        ChatOptions? chatOptions = agent.GetService<ChatClientAgentOptions>()?.ChatOptions;
        Assert.NotNull(chatOptions);

        chatOptions.Tools ??= [];
        chatOptions.Tools.Add(new StartPlanTool().Tool);

        // Second access should see the same reference and same tools
        ChatOptions? chatOptions2 = agent.GetService<ChatClientAgentOptions>()?.ChatOptions;
        Assert.Same(chatOptions, chatOptions2);
        Assert.Single(chatOptions2!.Tools!);
        Assert.Equal("start_plan", chatOptions2.Tools![0].Name);
    }

    [Fact]
    public void PlanningTools_AgentWithoutTools_ChatOptionsToolsIsNull_InitializesToEmptyList()
    {
        // An agent created with instructions but no tools has ChatOptions with Tools = null
        var stubClient = new StubChatClient();
        var agent = new ChatClientAgent(stubClient, "test instructions", "TestAgent");

        ChatOptions? chatOptions = agent.GetService<ChatClientAgentOptions>()?.ChatOptions;
        Assert.NotNull(chatOptions);

        // Tools is null initially when no tools were provided to the constructor
        // The ??= pattern should initialize it
        chatOptions.Tools ??= [];
        Assert.NotNull(chatOptions.Tools);
        Assert.Empty(chatOptions.Tools);

        // Now we can add tools
        chatOptions.Tools.Add(new StartPlanTool().Tool);
        Assert.Single(chatOptions.Tools);
    }

    /// <summary>Minimal chat client stub for testing agent construction.</summary>
    private sealed class StubChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "stub")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<ChatResponseUpdate>();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
