# Sub-agents for durable agents

## Overview

The sub-agent feature enables a durable agent to delegate tasks to other agents that run as independent durable entities. Sub-agents execute autonomously with their own conversation context and return results to the parent agent. Because sub-agents are durable entities, they can be scheduled on different compute nodes by the Durable Task Scheduler, enabling scalable multi-agent workloads.

## When to use sub-agents

Sub-agents are useful when:

- A task benefits from focused, independent execution (e.g., research, analysis, code review)
- You want to parallelize work across multiple agents
- An agent needs to "fork" itself to explore different approaches
- You want to isolate a subtask's context to reduce token usage in the parent conversation

## How sub-agents work

The sub-agent feature is implemented as a built-in tool (`run_sub_agent`) that can be registered on any durable agent. When the LLM decides to delegate a task, it calls the tool with a task description and optionally specifies which agent type to use.

### Execution flow

```
Parent Agent Entity (@coordinator@session-abc)
  └─ LLM returns tool_call: run_sub_agent(task="Analyze...", agent_name="researcher")
       └─ RunSubAgentTool:
            1. Validates nesting depth
            2. Calls OnBeforeSubAgentRun hook (if configured)
            3. Creates a new entity for the sub-agent
            4. Signals the entity with the task
            5. Polls for the response
            6. Calls OnAfterSubAgentRun hook (if configured)
            7. Returns result to parent as tool output
       └─ LLM receives result and continues conversation
```

Key characteristics:

- **Context isolation**: Sub-agents start with a clean conversation. They receive only the task description — not the parent's conversation history.
- **Durability**: Sub-agents are durable entities with their own persistent state and conversation history.
- **Distributed execution**: The Durable Task Scheduler can route sub-agent work items to any available compute node.
- **Result as tool output**: The sub-agent's final response text is returned to the parent as a standard tool call result, which the LLM naturally incorporates into its conversation.

## Configuration

### Basic setup

Register the `RunSubAgentTool` as a tool on your agent:

```csharp
using Microsoft.Agents.AI.DurableTask.BuiltInTools;

// Create the sub-agent tool with default options
RunSubAgentTool subAgentTool = new();

// Register it on an agent
AIAgent agent = new ChatClientAgent(
    chatClient,
    instructions: "You are a coordinator that can delegate tasks...",
    tools: [subAgentTool]);

// Register the agent with durable agents
services.ConfigureDurableAgents(
    options => options.AddAIAgent(agent));
```

### Options

The `RunSubAgentToolOptions` class provides the following configuration:

| Option | Default | Description |
|--------|---------|-------------|
| `MaxNestingDepth` | 2 | Maximum allowed nesting depth. Level 0 is the root agent, 1 is a child, 2 is a grandchild. |
| `Timeout` | 10 minutes | Maximum time to wait for a sub-agent to complete before returning a timeout error. |
| `OnBeforeSubAgentRun` | null | Callback invoked before the sub-agent is scheduled. Use for resource setup (e.g., sandbox creation). |
| `OnAfterSubAgentRun` | null | Callback invoked after the sub-agent completes, times out, or fails. Use for cleanup. Guaranteed to be called if `OnBeforeSubAgentRun` was called. |

```csharp
RunSubAgentTool subAgentTool = new(new RunSubAgentToolOptions
{
    MaxNestingDepth = 3,
    Timeout = TimeSpan.FromMinutes(15),
});
```

### Multiple agent types

Register multiple agent types so the parent can choose which sub-agent to delegate to:

```csharp
AIAgent coordinator = new ChatClientAgent(
    chatClient,
    instructions: "You coordinate work. Delegate research to 'researcher' and coding to 'coder'.",
    name: "coordinator",
    tools: [new RunSubAgentTool()]);

AIAgent researcher = new ChatClientAgent(
    chatClient,
    instructions: "You are a focused research agent.",
    name: "researcher");

AIAgent coder = new ChatClientAgent(
    chatClient,
    instructions: "You are a focused coding agent.",
    name: "coder");

services.ConfigureDurableAgents(options =>
{
    options.AddAIAgent(coordinator);
    options.AddAIAgent(researcher);
    options.AddAIAgent(coder);
});
```

When the coordinator's LLM calls `run_sub_agent`, it can specify `agent_name: "researcher"` or `agent_name: "coder"`. If `agent_name` is omitted, the tool defaults to the parent's own agent type (a "self-fork").

## Lifecycle hooks

Lifecycle hooks enable you to set up and tear down resources around sub-agent execution. A key use case is **sandbox management**: creating an isolated environment before the sub-agent runs and cleaning it up afterward.

```csharp
RunSubAgentTool subAgentTool = new(new RunSubAgentToolOptions
{
    OnBeforeSubAgentRun = async (context, ct) =>
    {
        // Create a sandbox for the sub-agent
        string sandboxId = await SandboxManager.CreateAsync(ct);
        context.Properties["sandboxId"] = sandboxId;

        Console.WriteLine($"Sandbox {sandboxId} created for sub-agent {context.SubAgentSessionId}");
    },
    OnAfterSubAgentRun = async (context, ct) =>
    {
        // Tear down the sandbox (always called if OnBeforeSubAgentRun was called)
        if (context.Properties.TryGetValue("sandboxId", out object? value) && value is string sandboxId)
        {
            await SandboxManager.DeleteAsync(sandboxId, ct);
            Console.WriteLine($"Sandbox {sandboxId} cleaned up (timed out: {context.TimedOut})");
        }
    },
});
```

### Hook context

Both hooks receive a context object with information about the sub-agent invocation:

| Property | Type | Description |
|----------|------|-------------|
| `ParentSessionId` | `AgentSessionId` | Session ID of the parent agent |
| `SubAgentSessionId` | `AgentSessionId` | Session ID assigned to the sub-agent |
| `AgentName` | `string` | Name of the agent type being used |
| `Task` | `string` | Task description delegated to the sub-agent |
| `NestingLevel` | `int` | Nesting level of the sub-agent (1 = direct child) |
| `Properties` | `IDictionary<string, object?>` | State bag for passing data between hooks |

The `OnAfterSubAgentRun` hook additionally receives:

| Property | Type | Description |
|----------|------|-------------|
| `Response` | `AgentResponse?` | The sub-agent's response (null if timed out or failed) |
| `Exception` | `Exception?` | The exception (if the sub-agent failed) |
| `TimedOut` | `bool` | Whether the sub-agent timed out |

## Sub-agent TTL

Sub-agent entities follow the same TTL rules as other durable agents. If the sub-agent's agent type has a TTL configured in `DurableAgentsOptions`, that TTL applies to the sub-agent entity. This ensures sub-agent state is eventually cleaned up even if the parent agent doesn't explicitly delete it.

## Nesting depth

Sub-agents can themselves create further sub-agents, up to the configured `MaxNestingDepth`. The nesting level is tracked automatically:

- **Level 0**: Root agent (the agent receiving user messages)
- **Level 1**: Direct child sub-agent
- **Level 2**: Grandchild sub-agent (default maximum)

If a sub-agent attempts to create another sub-agent beyond the maximum depth, the tool returns an error message to the LLM (not an exception), allowing the LLM to handle the situation gracefully.

## Best practices

1. **Include context in the task**: Since sub-agents don't inherit the parent's conversation history, include all necessary context in the task description. The LLM typically does this well when given appropriate instructions.

2. **Set appropriate timeouts**: Choose timeout values based on expected sub-agent execution time. For complex research tasks, a longer timeout may be needed.

3. **Use lifecycle hooks for resource management**: If sub-agents need sandboxed environments, use `OnBeforeSubAgentRun` / `OnAfterSubAgentRun` to manage their lifecycle.

4. **Limit nesting depth**: Deep nesting increases latency and complexity. The default of 2 levels is sufficient for most use cases.

5. **Name your agent types**: Give agents descriptive names so the LLM can make informed decisions about which agent type to delegate to.

## Samples

- [Console App: Sub-Agents](../../../dotnet/samples/04-hosting/DurableAgents/ConsoleApps/08_SubAgents/) — Interactive console demo with coordinator and researcher agents
- [Azure Functions: Sub-Agents](../../../dotnet/samples/04-hosting/DurableAgents/AzureFunctions/09_SubAgents/) — HTTP-triggered endpoint with sub-agent support
