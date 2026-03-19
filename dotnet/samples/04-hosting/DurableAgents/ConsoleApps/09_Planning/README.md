# Planning Sample

This sample demonstrates how to use the plan-and-execute feature to enable a durable agent to decompose complex tasks into structured multi-step plans and execute them durably. The agent generates a DAG-based plan, validates it, and runs it as a checkpointed durable orchestration.

Full documentation on planning can be found [here](../../../../../../docs/features/durable-agents/durable-agents-planning.md).

## Key Concepts Demonstrated

- Using `AddPlanningSupport()` to register the 5 built-in planning tools
- Configuring `PlanExecutionOptions` with step limits, allowlists, and re-planning
- LLM-driven plan generation and execution
- Monitoring task progress via `get_task_status`
- Multiple agent types (planner + researcher) where the planner can delegate via `invoke_agent` steps

## Environment Setup

See the [README.md](../README.md) file in the parent directory for more information on how to configure the environment, including how to install and run common sample dependencies.

## Running the Sample

With the environment setup, you can run the sample:

```bash
cd dotnet/samples/04-hosting/DurableAgents/ConsoleApps/09_Planning
dotnet run --framework net10.0
```

The app will prompt you for input. Try asking a complex question that requires multiple steps:

```text
=== Planning Sample ===
Compare the pros and cons of three major cloud providers and produce a recommendation
```

The planner agent will:

1. Generate a plan with parallel research steps and a synthesis step
2. Execute the plan as a durable orchestration
3. Monitor progress and relay results to you

## How It Works

1. The **planner** agent is registered with `AddPlanningSupport()`, which adds 5 built-in tools
2. When the planner receives a complex request, the LLM generates an `AgentPlan` JSON and calls `start_plan`
3. The plan is validated (DAG structure, allowlists, step limits) and scheduled as a durable orchestration
4. The orchestration executes steps in topological order, running independent steps in parallel
5. Step results flow through the DAG via `{step_id}` references
6. The planner can check progress with `get_task_status` and report back to the user

## Viewing Agent and Task State

You can view the state of the agent and its plan tasks in the Durable Task Scheduler dashboard:

1. Open your browser and navigate to `http://localhost:8082`
2. You will see entities for the planner agent and orchestration instances for each plan task
3. Each plan task shows progress, completed steps, and results
