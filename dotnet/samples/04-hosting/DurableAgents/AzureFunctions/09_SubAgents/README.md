# Sub-Agent Sample (Azure Functions)

This sample demonstrates how to use the built-in `run_sub_agent` tool with Azure Functions hosting. A coordinator agent can delegate tasks to a researcher sub-agent, with both agents running as durable entities.

## Key Concepts Demonstrated

- Using `RunSubAgentTool` with Azure Functions hosting
- HTTP-triggered endpoints for agents with sub-agent support
- Multiple agent types (coordinator + researcher) in the same function app
- Sub-agent results incorporated as tool call results in the parent conversation
- Thread ID support for multi-turn conversations with sub-agent delegation

## Environment Setup

See the [README.md](../README.md) file in the parent directory for more information on how to configure the environment, including how to install and run common sample dependencies.

## Running the Sample

With the environment setup, run the Azure Functions app:

```bash
cd dotnet/samples/04-hosting/DurableAgents/AzureFunctions/09_SubAgents
func start
```

Then send requests using the `demo.http` file (with VS Code REST Client) or `curl`:

```bash
curl -X POST http://localhost:7071/api/agents/coordinator/run \
  -H "Content-Type: text/plain" \
  -d "Compare the pros and cons of microservices vs monolithic architecture."
```

The coordinator agent will delegate the research to a sub-agent and return a synthesized response.

## Available Endpoints

| Endpoint | Description |
|----------|-------------|
| `POST /api/agents/coordinator/run` | Send messages to the coordinator (can delegate to sub-agents) |
| `POST /api/agents/researcher/run` | Send messages directly to the researcher agent |

## Viewing Agent State

You can view the state of both agents in the Durable Task Scheduler dashboard:

1. Open your browser and navigate to `http://localhost:8082`
2. You will see entities for the coordinator and any sub-agents that were created
3. Each sub-agent has its own conversation history and state
