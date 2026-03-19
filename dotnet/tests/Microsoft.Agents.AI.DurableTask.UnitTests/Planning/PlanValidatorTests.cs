// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.DurableTask.Planning;

namespace Microsoft.Agents.AI.DurableTask.UnitTests.Planning;

public sealed class PlanValidatorTests
{
    [Fact]
    public void ValidPlan_PassesValidation()
    {
        AgentPlan plan = CreateSimplePlan();
        List<string> errors = PlanValidator.Validate(plan);
        Assert.Empty(errors);
    }

    [Fact]
    public void EmptySteps_FailsValidation()
    {
        AgentPlan plan = new() { Goal = "Do something", Steps = [] };
        List<string> errors = PlanValidator.Validate(plan);
        Assert.Contains(errors, e => e.Contains("at least one step"));
    }

    [Fact]
    public void DuplicateStepIds_Detected()
    {
        AgentPlan plan = new()
        {
            Goal = "Test",
            Steps =
            [
                new PlanStep
                {
                    Id = "step1",
                    Description = "First",
                    Action = new PromptAction { Prompt = "Do A" },
                },
                new PlanStep
                {
                    Id = "step1",
                    Description = "Duplicate",
                    Action = new PromptAction { Prompt = "Do B" },
                },
            ],
        };

        List<string> errors = PlanValidator.Validate(plan);
        Assert.Contains(errors, e => e.Contains("Duplicate step id 'step1'"));
    }

    [Fact]
    public void CycleDetected()
    {
        AgentPlan plan = new()
        {
            Goal = "Test cycle",
            Steps =
            [
                new PlanStep
                {
                    Id = "a",
                    Description = "Step A",
                    Action = new PromptAction { Prompt = "A" },
                    DependsOn = ["b"],
                },
                new PlanStep
                {
                    Id = "b",
                    Description = "Step B",
                    Action = new PromptAction { Prompt = "B" },
                    DependsOn = ["a"],
                },
            ],
        };

        List<string> errors = PlanValidator.Validate(plan);
        Assert.Contains(errors, e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingDependencyReference_Detected()
    {
        AgentPlan plan = new()
        {
            Goal = "Test missing dep",
            Steps =
            [
                new PlanStep
                {
                    Id = "step1",
                    Description = "Step 1",
                    Action = new PromptAction { Prompt = "Do something" },
                    DependsOn = ["nonexistent"],
                },
            ],
        };

        List<string> errors = PlanValidator.Validate(plan);
        Assert.Contains(errors, e => e.Contains("unknown step 'nonexistent'"));
    }

    [Fact]
    public void StepIdReference_ToNonDependency_Detected()
    {
        AgentPlan plan = new()
        {
            Goal = "Test ref to non-dep",
            Steps =
            [
                new PlanStep
                {
                    Id = "step1",
                    Description = "Step 1",
                    Action = new PromptAction { Prompt = "Compute" },
                },
                new PlanStep
                {
                    Id = "step2",
                    Description = "Step 2",
                    Action = new PromptAction { Prompt = "Use {step1}" },
                    // No DependsOn — step1 is not a dependency
                },
            ],
        };

        List<string> errors = PlanValidator.Validate(plan);
        Assert.Contains(errors, e => e.Contains("{step1}") && e.Contains("not a dependency"));
    }

    [Fact]
    public void UnknownToolName_Detected()
    {
        AgentPlan plan = new()
        {
            Goal = "Test unknown tool",
            Steps =
            [
                new PlanStep
                {
                    Id = "step1",
                    Description = "Use unknown tool",
                    Action = new InvokeToolAction { ToolName = "unknown_tool" },
                },
            ],
        };

        List<string> errors = PlanValidator.Validate(plan, registeredToolNames: ["known_tool"]);
        Assert.Contains(errors, e => e.Contains("'unknown_tool'") && e.Contains("not registered"));
    }

    [Fact]
    public void ToolNotInAllowlist_Detected()
    {
        PlanExecutionOptions options = new()
        {
            AllowedTools = new HashSet<string>(StringComparer.Ordinal) { "allowed_tool" },
        };

        AgentPlan plan = new()
        {
            Goal = "Test tool allowlist",
            Steps =
            [
                new PlanStep
                {
                    Id = "step1",
                    Description = "Use disallowed tool",
                    Action = new InvokeToolAction { ToolName = "disallowed_tool" },
                },
            ],
        };

        List<string> errors = PlanValidator.Validate(plan, options: options);
        Assert.Contains(errors, e => e.Contains("'disallowed_tool'") && e.Contains("not in the allowed tools list"));
    }

    [Fact]
    public void UnknownAgentName_Detected()
    {
        AgentPlan plan = new()
        {
            Goal = "Test unknown agent",
            Steps =
            [
                new PlanStep
                {
                    Id = "step1",
                    Description = "Call unknown agent",
                    Action = new InvokeAgentAction
                    {
                        AgentName = "unknown_agent",
                        Task = "Do something",
                    },
                },
            ],
        };

        List<string> errors = PlanValidator.Validate(plan, registeredAgentNames: ["known_agent"]);
        Assert.Contains(errors, e => e.Contains("'unknown_agent'") && e.Contains("not registered"));
    }

    [Fact]
    public void AgentNotInAllowlist_Detected()
    {
        PlanExecutionOptions options = new()
        {
            AllowedAgents = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "allowed_agent" },
        };

        AgentPlan plan = new()
        {
            Goal = "Test agent allowlist",
            Steps =
            [
                new PlanStep
                {
                    Id = "step1",
                    Description = "Call disallowed agent",
                    Action = new InvokeAgentAction
                    {
                        AgentName = "disallowed_agent",
                        Task = "Do something",
                    },
                },
            ],
        };

        List<string> errors = PlanValidator.Validate(plan, options: options);
        Assert.Contains(errors, e => e.Contains("'disallowed_agent'") && e.Contains("not in the allowed agents list"));
    }

    [Fact]
    public void InvalidIsoDuration_Detected()
    {
        AgentPlan plan = new()
        {
            Goal = "Test invalid duration",
            Steps =
            [
                new PlanStep
                {
                    Id = "step1",
                    Description = "Sleep badly",
                    Action = new SleepAction { Duration = "not-a-duration" },
                },
            ],
        };

        List<string> errors = PlanValidator.Validate(plan);
        Assert.Contains(errors, e => e.Contains("invalid ISO 8601 duration"));
    }

    [Fact]
    public void MaxStepsExceeded_Detected()
    {
        PlanExecutionOptions options = new() { MaxSteps = 2 };

        AgentPlan plan = new()
        {
            Goal = "Test max steps",
            Steps =
            [
                new PlanStep { Id = "a", Description = "A", Action = new PromptAction { Prompt = "A" } },
                new PlanStep { Id = "b", Description = "B", Action = new PromptAction { Prompt = "B" } },
                new PlanStep { Id = "c", Description = "C", Action = new PromptAction { Prompt = "C" } },
            ],
        };

        List<string> errors = PlanValidator.Validate(plan, options: options);
        Assert.Contains(errors, e => e.Contains("exceeding the maximum of 2"));
    }

    [Fact]
    public void ValidPlanWithParallelSteps_PassesValidation()
    {
        AgentPlan plan = new()
        {
            Goal = "Parallel work",
            Steps =
            [
                new PlanStep
                {
                    Id = "fetch_a",
                    Description = "Fetch data A",
                    Action = new InvokeToolAction { ToolName = "fetch" },
                },
                new PlanStep
                {
                    Id = "fetch_b",
                    Description = "Fetch data B",
                    Action = new InvokeToolAction { ToolName = "fetch" },
                },
                new PlanStep
                {
                    Id = "merge",
                    Description = "Merge results",
                    Action = new PromptAction { Prompt = "Merge {fetch_a} and {fetch_b}" },
                    DependsOn = ["fetch_a", "fetch_b"],
                },
            ],
        };

        List<string> errors = PlanValidator.Validate(plan);
        Assert.Empty(errors);
    }

    private static AgentPlan CreateSimplePlan()
    {
        return new AgentPlan
        {
            Goal = "Complete a simple task",
            Steps =
            [
                new PlanStep
                {
                    Id = "step1",
                    Description = "First step",
                    Action = new PromptAction { Prompt = "Do the first thing" },
                },
                new PlanStep
                {
                    Id = "step2",
                    Description = "Second step",
                    Action = new PromptAction { Prompt = "Use {step1} to do the second thing" },
                    DependsOn = ["step1"],
                },
            ],
        };
    }
}
