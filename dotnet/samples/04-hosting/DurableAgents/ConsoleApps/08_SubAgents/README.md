# Sub-Agent Sample

This sample demonstrates how to use the built-in `run_sub_agent` tool to enable durable agents to delegate tasks to sub-agents. Sub-agents run as independent durable entities and can be scheduled on different compute nodes.

Full documentation on sub-agents can be found [here](../../../../../../docs/features/durable-agents/durable-agents-sub-agents.md).

## Key Concepts Demonstrated

- Using `RunSubAgentTool` to enable sub-agent delegation
- Registering multiple agent types (coordinator + researcher)
- Lifecycle hooks (`OnBeforeSubAgentRun` / `OnAfterSubAgentRun`) for resource setup/teardown
- Configuring nesting depth limits and timeouts
- Sub-agent results incorporated as tool call results in the parent conversation

## Environment Setup

See the [README.md](../README.md) file in the parent directory for more information on how to configure the environment, including how to install and run common sample dependencies.

## Running the Sample

With the environment setup, you can run the sample:

```bash
cd dotnet/samples/04-hosting/DurableAgents/ConsoleApps/08_SubAgents
dotnet run --framework net10.0
```

The app will prompt you for input. The coordinator agent will delegate research tasks to the researcher sub-agent when appropriate:

```text
=== Sub-Agent Console Sample ===
The coordinator agent can delegate tasks to a 'researcher' sub-agent.
Try asking complex questions that require in-depth analysis.
Type 'exit' to quit.

You: Compare the pros and cons of microservices vs monolithic architecture
  [Hook] Creating sub-agent 'researcher' (session: a1b2c3d4...)
  [Hook] Sub-agent 'researcher' completed
Coordinator: Based on the researcher's analysis, here's a summary...

You: exit
```

## How It Works

1. The **coordinator** agent is configured with a `RunSubAgentTool`
2. When the coordinator receives a complex question, the LLM decides to call the `run_sub_agent` tool
3. The tool creates a new durable entity for the **researcher** agent with a fresh conversation
4. The researcher processes the task autonomously and returns its result
5. The coordinator receives the result as a tool call response and synthesizes the final answer

## Viewing Agent State

You can view the state of both the coordinator and researcher agents in the Durable Task Scheduler dashboard:

1. Open your browser and navigate to `http://localhost:8082`
2. You will see entities for both the coordinator and any sub-agents that were created
3. Each sub-agent has its own conversation history and state
