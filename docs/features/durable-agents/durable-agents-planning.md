# Plan-and-execute for durable agents

## Overview

The plan-and-execute feature enables a durable agent to decompose complex, long-horizon tasks into structured multi-step plans and execute them durably. When given a task like "research three competing products, summarize each, and produce a comparison report," the agent can generate a plan with parallel research steps feeding into a final synthesis step, then execute that plan with full checkpoint and retry support.

This is useful when:

- A task involves multiple sequential or parallel sub-tasks with data dependencies
- Work may span minutes, hours, or days (e.g., waiting for human approvals)
- You need visibility into progress and the ability to cancel or provide input mid-execution
- Failure of one step should not lose the results of previously completed steps

## Terminology

| Term | Meaning |
| --- | --- |
| **Plan** | A document (JSON) describing a DAG of steps with a stated goal. The LLM generates this; it is validated before execution. |
| **Task** | A running execution of a plan. Each task has an ID, status, and progress. One agent session can have multiple concurrent tasks. |
| **Step** | A single node in the plan DAG. Steps declare dependencies on other steps and are executed in topological order. |
| **Action** | What a step does: invoke a tool, invoke an agent, sleep, prompt the LLM, or wait for human input. |

## How it works

```
User sends complex request
    ↓
Agent (with planning tools) forwards to LLM
    ↓
LLM generates AgentPlan JSON and calls start_plan
    ↓
StartPlanTool validates plan (schema, DAG acyclicity, allowlists)
    ↓
Durable orchestration scheduled → returns task ID to LLM
    ↓
Orchestration executes DAG:
  • Steps with no dependencies start immediately
  • Steps with dependencies wait for predecessors
  • Results flow via {step_id} references
  • Failures trigger re-planning (if enabled)
    ↓
Agent uses get_task_status to poll progress
Agent uses send_task_input to provide human input
Agent uses cancel_task to abort
```

The agent remains in the conversation loop throughout. The LLM decides when to check status, relay results to the user, or provide input — the planning tools are just tools the LLM can call.

## Built-in tools

When you call `options.AddPlanningSupport(...)`, five tools are registered on the agent:

| Tool | Description |
| --- | --- |
| `start_plan` | Accepts a plan JSON string, validates it, schedules a durable orchestration, and returns a task ID. |
| `list_tasks` | Returns all active tasks for the current agent session. |
| `get_task_status` | Returns the status, progress, completed steps, pending inputs, and results for a specific task. |
| `send_task_input` | Sends human-provided input to a task that is waiting for it (i.e., a `wait_for_input` step). |
| `cancel_task` | Terminates a running task. |

## Step types

Each step in a plan has an `action` with a `type` discriminator:

### `invoke_tool`

Calls a registered tool by name with optional arguments.

```json
{
  "id": "search",
  "description": "Search for product reviews",
  "action": {
    "type": "invoke_tool",
    "tool_name": "web_search",
    "arguments": { "query": "Product X reviews 2025" }
  }
}
```

### `invoke_agent`

Delegates a task to another registered durable agent.

```json
{
  "id": "analyze",
  "description": "Analyze search results",
  "action": {
    "type": "invoke_agent",
    "agent_name": "researcher",
    "task": "Analyze the following search results: {search}"
  },
  "depends_on": ["search"]
}
```

### `sleep`

Pauses execution for a specified ISO 8601 duration.

```json
{
  "id": "wait",
  "description": "Wait 1 hour before checking",
  "action": { "type": "sleep", "duration": "PT1H" }
}
```

### `prompt`

Sends a prompt to the LLM and captures the response.

```json
{
  "id": "summarize",
  "description": "Summarize all findings",
  "action": {
    "type": "prompt",
    "prompt": "Summarize: {research_a}, {research_b}"
  },
  "depends_on": ["research_a", "research_b"]
}
```

### `wait_for_input`

Pauses the step until human input is provided via `send_task_input`.

```json
{
  "id": "approval",
  "description": "Wait for manager approval",
  "action": {
    "type": "wait_for_input",
    "description": "Please approve or reject the proposal",
    "input_name": "manager_approval"
  }
}
```

## Step result references

Steps can reference the results of predecessor steps using the `{step_id}` syntax. This enables data to flow through the plan DAG.

### Resolution rules

- **In strings**: `{step_id}` is replaced with the step's text result. If the step produced a JSON result, it is serialized to a string.
- **In JSON argument values**: An exact match `"{step_id}"` is replaced with the step's structured JSON result (object embedding). A partial match like `"prefix {step_id} suffix"` is replaced as a string.
- **Validation**: The validator ensures all `{step_id}` references point to steps that are in the referencing step's transitive dependencies. This prevents references to steps that might not have completed yet.

### Example

```json
{
  "goal": "Research and summarize a topic",
  "steps": [
    {
      "id": "search",
      "description": "Search the web",
      "action": {
        "type": "invoke_tool",
        "tool_name": "web_search",
        "arguments": { "query": "latest AI breakthroughs" }
      }
    },
    {
      "id": "summarize",
      "description": "Summarize search results",
      "action": {
        "type": "prompt",
        "prompt": "Summarize these search results: {search}"
      },
      "depends_on": ["search"]
    }
  ]
}
```

In this example, `{search}` in the prompt step is replaced with the text result of the `search` step at execution time.

## Configuration

### PlanExecutionOptions

Configure planning behavior via `PlanExecutionOptions`:

| Option | Default | Description |
| --- | --- | --- |
| `EnableReplanning` | `true` | Allow the agent to revise the plan if a step fails |
| `MaxReplanAttempts` | 3 | Maximum number of re-planning attempts |
| `MaxSteps` | 20 | Maximum number of steps allowed in a plan |
| `MaxConcurrentTasks` | 5 | Maximum concurrent plan tasks per agent session |
| `MaxAgentNestingDepth` | 2 | Maximum nesting depth for `invoke_agent` steps |
| `WaitForInputTimeout` | 7 days | Timeout for `wait_for_input` steps |
| `ToolInvocationTimeout` | 10 minutes | Timeout for `invoke_tool` steps |
| `AgentInvocationTimeout` | 30 minutes | Timeout for `invoke_agent` steps |
| `AllowedTools` | `null` (all) | Allowlist of tool names the plan may invoke. `null` means all registered tools are allowed. |
| `AllowedAgents` | `null` (all) | Allowlist of agent names the plan may delegate to. `null` means all registered agents are allowed. |
| `ToolRetryPolicy` | `null` | Durable Task retry policy for tool invocation failures |

## Registration

Enable planning support on a durable agent with a single call:

```csharp
services.ConfigureDurableAgents(
    options =>
    {
        options.AddAIAgent(myAgent);

        // Enable planning with default options
        options.AddPlanningSupport();

        // Or customize options
        options.AddPlanningSupport(planOptions =>
        {
            planOptions.MaxSteps = 30;
            planOptions.MaxConcurrentTasks = 3;
            planOptions.AllowedTools = new HashSet<string> { "web_search", "file_read" };
            planOptions.AllowedAgents = new HashSet<string> { "researcher" };
            planOptions.EnableReplanning = true;
            planOptions.WaitForInputTimeout = TimeSpan.FromDays(14);
        });
    });
```

This registers the five built-in tools (`start_plan`, `list_tasks`, `get_task_status`, `send_task_input`, `cancel_task`) and the plan execution orchestration.

## When to use planning

### Good fit

- **Multi-step research tasks** — gather data from multiple sources, then synthesize
- **Approval workflows** — execute steps that require human sign-off at specific points
- **Parallel independent work** — fan out to multiple tools or agents, then aggregate
- **Long-running operations** — tasks that span hours or days with intermediate checkpoints
- **Tasks with clear decomposition** — the work can be expressed as discrete steps with data dependencies

### Not a good fit

- **Simple Q&A** — a single LLM call suffices; planning adds unnecessary overhead
- **Real-time streaming** — plan execution is checkpoint-based, not streaming
- **Highly dynamic tasks** — if the next step depends entirely on unpredictable intermediate results, a conversational loop is more natural
- **Tight latency requirements** — each step involves durable checkpoint overhead

## Hierarchical plans

For very complex tasks, a step can use `invoke_agent` to delegate to another agent that itself has planning support. The inner agent can generate and execute its own sub-plan. The `MaxAgentNestingDepth` option controls how deep this nesting can go.

```json
{
  "goal": "Complete quarterly report",
  "steps": [
    {
      "id": "financials",
      "description": "Delegate financial analysis to the finance agent",
      "action": {
        "type": "invoke_agent",
        "agent_name": "finance_agent",
        "task": "Produce Q3 financial summary with revenue breakdown"
      }
    },
    {
      "id": "engineering",
      "description": "Delegate engineering update to the eng agent",
      "action": {
        "type": "invoke_agent",
        "agent_name": "engineering_agent",
        "task": "Produce Q3 engineering update with shipped features"
      }
    },
    {
      "id": "compile",
      "description": "Compile the quarterly report",
      "action": {
        "type": "prompt",
        "prompt": "Compile a quarterly report from: Finance: {financials}, Engineering: {engineering}"
      },
      "depends_on": ["financials", "engineering"]
    }
  ]
}
```

## Planning vs. Workflows

The planning feature and [deterministic orchestrations](README.md#deterministic-multi-agent-orchestrations) (workflows) serve different purposes:

| Aspect | Plan-and-execute | Deterministic workflows |
| --- | --- | --- |
| **Who defines the steps?** | The LLM, at runtime | The developer, at build time |
| **Structure** | Dynamic DAG in JSON | Code (C# orchestrator function) |
| **Determinism** | Non-deterministic (LLM-generated) | Fully deterministic (in terms of possible paths) |
| **Flexibility** | High — adapts to the task | Low — fixed logic |
| **Visibility** | Task status, step results, progress | Orchestration history, custom status |
| **Re-planning** | Built-in on failure | Must be coded explicitly |
| **Human input** | `wait_for_input` step type | `WaitForExternalEvent` API |
| **Best for** | Open-ended, user-driven tasks | Repeatable, well-defined processes |

They can be combined: a workflow can call an agent that uses planning, or a plan step can trigger a workflow via `invoke_tool`.

## MCP Tasks alignment

The planning tools follow patterns compatible with the [MCP Tasks](https://modelcontextprotocol.io/specification/draft/basic/utilities/tasks) interaction model:

- `start_plan` creates a task and returns an ID (similar to MCP task creation)
- `get_task_status` polls for progress (similar to MCP task status)
- `send_task_input` provides input to a waiting task (similar to MCP task input)
- `cancel_task` terminates a task (similar to MCP task cancellation)

This alignment means agents built with planning support can more easily interoperate with MCP-compatible clients and servers.

## Security considerations

### Allowlists

Use `AllowedTools` and `AllowedAgents` to restrict what the LLM-generated plan can invoke. Without allowlists, the plan can call any registered tool or agent, which may not be appropriate for all scenarios.

```csharp
options.AddPlanningSupport(planOptions =>
{
    // Only allow specific tools in plans
    planOptions.AllowedTools = new HashSet<string> { "web_search", "calculator" };

    // Only allow specific agents in plans
    planOptions.AllowedAgents = new HashSet<string> { "researcher" };
});
```

### Prompt injection

Because the plan is generated by the LLM, it is susceptible to prompt injection attacks. An adversarial user could craft input that causes the LLM to generate a plan that invokes unintended tools or agents. Mitigations:

- **Allowlists**: Restrict which tools and agents can be invoked
- **Validation**: The plan validator checks tool/agent names, step limits, and DAG structure before execution
- **Max steps**: Limit the plan size to prevent resource exhaustion
- **Review**: For sensitive operations, include `wait_for_input` steps that require human approval before proceeding

## Example plan JSON

A complete plan for a product comparison task:

```json
{
  "goal": "Compare three cloud providers and produce a recommendation report",
  "steps": [
    {
      "id": "research_aws",
      "description": "Research AWS offerings",
      "action": {
        "type": "invoke_tool",
        "tool_name": "web_search",
        "arguments": { "query": "AWS cloud services pricing features 2025" }
      }
    },
    {
      "id": "research_azure",
      "description": "Research Azure offerings",
      "action": {
        "type": "invoke_tool",
        "tool_name": "web_search",
        "arguments": { "query": "Azure cloud services pricing features 2025" }
      }
    },
    {
      "id": "research_gcp",
      "description": "Research GCP offerings",
      "action": {
        "type": "invoke_tool",
        "tool_name": "web_search",
        "arguments": { "query": "GCP cloud services pricing features 2025" }
      }
    },
    {
      "id": "compare",
      "description": "Produce comparison table",
      "action": {
        "type": "prompt",
        "prompt": "Create a detailed comparison table from: AWS: {research_aws}, Azure: {research_azure}, GCP: {research_gcp}"
      },
      "depends_on": ["research_aws", "research_azure", "research_gcp"]
    },
    {
      "id": "approval",
      "description": "Get stakeholder approval on the comparison",
      "action": {
        "type": "wait_for_input",
        "description": "Please review the comparison table and approve or request changes",
        "input_name": "stakeholder_review"
      },
      "depends_on": ["compare"]
    },
    {
      "id": "final_report",
      "description": "Produce final recommendation report",
      "action": {
        "type": "prompt",
        "prompt": "Based on the comparison {compare} and stakeholder feedback {approval}, write a final recommendation report."
      },
      "depends_on": ["compare", "approval"]
    }
  ]
}
```

In this plan:

1. Three research steps run **in parallel** (no dependencies between them)
2. The `compare` step waits for all three to complete, then synthesizes results
3. The `approval` step pauses for human input
4. The `final_report` step incorporates both the comparison and the feedback

## Samples

- [Console App: Planning](../../../dotnet/samples/04-hosting/DurableAgents/ConsoleApps/09_Planning/) — Interactive console demo showing plan generation and execution
- [Azure Functions: Planning](../../../dotnet/samples/04-hosting/DurableAgents/AzureFunctions/10_Planning/) — HTTP-triggered endpoint with planning support
