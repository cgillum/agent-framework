---
status: proposed
contact: cgillum
date: 2026-03-14
---

# DAG-Based Plan-and-Execute for Durable Agents

## Context and Problem Statement

Complex, long-horizon tasks are unreliable when executed in a single LLM turn. The agent may
lose track of steps, fail partway through, or produce inconsistent results. Existing durable
agent orchestration patterns (chaining, fan-out/fan-in, conditionals) require developers to
write explicit orchestration code — the LLM has no ability to autonomously decompose a complex
task into a structured execution plan.

We need a mechanism where a durable agent can generate a structured execution plan as a DAG,
execute that plan reliably as a durable orchestration, adapt mid-execution if steps fail, and
pause for human input at designated points.

## Decision Drivers

- Agents should be able to autonomously plan and execute complex, multi-step tasks
- Plan execution must be durable (survive crashes, scale-to-zero, multi-day execution)
- The agent entity must never be blocked during plan execution
- The design should align with the MCP Tasks specification for industry interoperability
- Task IDs must be recoverable even if LLM context is compacted or lost
- The feature must integrate cleanly with existing Durable Agents infrastructure
- Security: tool and agent invocations in plans must be controllable via allowlists
- The design should be conceptually simple and constrained for LLM generation reliability

## Considered Options

1. **Async built-in tools (MCP Tasks–aligned)**
2. Synchronous blocking tool
3. Build on existing Workflows system
4. New agent type (DurablePlanningAgent)
5. Orchestration extension methods only

## Decision Outcome

Chosen option: **"Async built-in tools (MCP Tasks–aligned)"**, because it is the only option
that satisfies all decision drivers simultaneously — non-blocking execution, durability,
LLM agency, MCP alignment, and entity state persistence for task ID recovery.

### Consequences

- Good, because the agent entity is never blocked — plan execution runs as an independent
  orchestration, supporting plans that last hours or days
- Good, because task IDs are persisted in entity state, surviving LLM context compaction
- Good, because the design aligns with MCP Tasks (`tasks/list`, `tasks/get`, `tasks/cancel`,
  `input_required` status), enabling future interoperability
- Good, because the existing long-running tools pattern (`.NET-only, never ported to Python`)
  is superseded by a more structured, validated, and observable approach
- Good, because plan validation catches errors before execution begins
- Neutral, because five built-in tools adds surface area, but each maps 1:1 to an MCP Tasks
  operation, making the mental model clear
- Bad, because `invoke_tool` steps run as activities without `DurableAgentContext`, so tools
  that depend on scheduling orchestrations from within tool calls will not work in plan steps
  (mitigated: use `invoke_agent` or plan-level step types instead)

## Pros and Cons of the Options

### Async built-in tools (MCP Tasks–aligned)

Five built-in tools: `start_plan`, `list_tasks`, `get_task_status`, `send_task_input`,
`cancel_task`. The LLM generates a plan as structured JSON, `start_plan` validates it and
schedules a durable orchestration, returning immediately with a task ID. The orchestration
executes the DAG with fan-out/fan-in, calling back to the agent for `prompt` and re-planning
steps. `wait_for_input` steps pause the orchestration until the agent raises an external event
via `send_task_input`.

- Good, because the agent entity is never blocked
- Good, because plans can run for hours/days (durable timers, external events)
- Good, because task IDs are in entity state, not just LLM context
- Good, because MCP Tasks alignment enables interoperability
- Good, because the LLM decides when to plan (agency preserved)
- Neutral, because five tools is more surface area than one

### Synchronous blocking tool

A single `execute_plan` tool that blocks the entity until the orchestration completes.

- Good, because simple — one tool, one call
- Bad, because the entity is locked for the entire plan duration
- Bad, because `prompt` steps need the entity, creating a deadlock
- Bad, because Azure Functions invocations timeout after minutes
- Bad, because LLM tool calls are not designed for multi-hour blocking

### Build on existing Workflows system

Convert LLM-generated plans into `Workflow` instances from `Microsoft.Agents.AI.Workflows`.

- Good, because reuses existing DAG execution infrastructure
- Bad, because Workflows are designed for compile-time, developer-written, typed DAGs
- Bad, because Workflows' protocol system (message handlers, send/yield types) is overkill
- Bad, because Workflows run in-process without durability guarantees
- Bad, because Workflows are immutable once built (no re-planning)

### New agent type (DurablePlanningAgent)

Wrap a regular agent in a `DurablePlanningAgent` that automatically plans before executing.

- Good, because clean abstraction
- Bad, because removes LLM agency — every task goes through planning
- Bad, because less composable with existing orchestration patterns

### Orchestration extension methods only

Extension methods on `TaskOrchestrationContext`: `GeneratePlanAsync`, `ExecutePlanAsync`.

- Good, because clean developer API
- Bad, because the developer decides when to plan, not the LLM
- Neutral, because included as a supplemental API alongside the tool-based approach

## More Information

- [MCP Tasks specification](https://modelcontextprotocol.io/specification/draft/basic/utilities/tasks)
- [Design document](../features/durable-agents/durable-agents-planning.md)
- Existing sample `06_LongRunningTools` demonstrates the schedule → poll → raise event pattern
  that this feature formalizes and supersedes
- The long-running tools pattern was never implemented in Python, confirming it as a .NET-only
  one-off that this feature replaces with a structured, cross-platform–ready design
