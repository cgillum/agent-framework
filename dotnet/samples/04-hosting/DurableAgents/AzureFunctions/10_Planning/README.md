# Planning Sample (Azure Functions)

This sample demonstrates how to use the plan-and-execute feature with Azure Functions hosting. A planner agent can decompose complex tasks into DAG-based plans that execute durably, with support for parallel steps, human input, and automatic re-planning.

Full documentation on planning can be found [here](../../../../../../docs/features/durable-agents/durable-agents-planning.md).

## Key Concepts Demonstrated

- Using `AddPlanningSupport()` with Azure Functions hosting
- HTTP-triggered endpoints for agents with planning support
- LLM-driven plan generation, validation, and durable execution
- Multiple agent types (planner + researcher) where the planner can delegate via `invoke_agent` steps
- Task monitoring, human input, and cancellation via built-in tools
- Thread ID support for multi-turn conversations

## Environment Setup

See the [README.md](../README.md) file in the parent directory for more information on how to configure the environment, including how to install and run common sample dependencies.

## Running the Sample

With the environment setup, run the Azure Functions app:

```bash
cd dotnet/samples/04-hosting/DurableAgents/AzureFunctions/10_Planning
func start
```

Then send requests using `curl`:

Bash (Linux/macOS/WSL):

```bash
curl -i -X POST http://localhost:7071/api/agents/planner/run \
    -D headers.txt \
    -H "Content-Type: text/plain" \
    -d 'Compare the pros and cons of three major cloud providers and produce a recommendation report'

# Save the thread ID to a variable
threadId=$(cat headers.txt | grep "x-ms-thread-id" | cut -d' ' -f2)
echo "Thread ID: $threadId"
```

PowerShell:

```powershell
Invoke-RestMethod -Method Post `
    -Uri http://localhost:7071/api/agents/planner/run `
    -ResponseHeadersVariable ResponseHeaders `
    -ContentType text/plain `
    -Body 'Compare the pros and cons of three major cloud providers and produce a recommendation report'

$threadId = $ResponseHeaders['x-ms-thread-id']
Write-Host "Thread ID: $threadId"
```

The planner agent will generate a plan with parallel research steps, execute it as a durable orchestration, and return the results.

To check on a running plan or continue the conversation:

```bash
curl -X POST "http://localhost:7071/api/agents/planner/run?thread_id=$threadId" \
    -H "Content-Type: text/plain" \
    -d 'What is the status of the plan you started?'
```

If a plan step is waiting for human input, you can provide it:

```bash
curl -X POST "http://localhost:7071/api/agents/planner/run?thread_id=$threadId" \
    -H "Content-Type: text/plain" \
    -d 'Approve the comparison report and proceed with the final recommendation'
```

## Available Endpoints

| Endpoint | Description |
|----------|-------------|
| `POST /api/agents/planner/run` | Send messages to the planner agent (can create and monitor plans) |
| `POST /api/agents/researcher/run` | Send messages directly to the researcher agent |

## Viewing Agent and Task State

You can view the state of agents and plan tasks in the Durable Task Scheduler dashboard:

1. Open your browser and navigate to `http://localhost:8082`
2. You will see entities for the planner agent and orchestration instances for each plan task
3. Each plan task shows progress, completed steps, and results
