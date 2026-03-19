// Copyright (c) Microsoft. All rights reserved.

using System.Text.RegularExpressions;
using System.Xml;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Validates an <see cref="AgentPlan"/> for structural correctness, acyclicity,
/// reference validity, and compliance with execution options.
/// </summary>
public static partial class PlanValidator
{
    /// <summary>
    /// Validates the given plan and returns a list of validation errors.
    /// An empty list indicates the plan is valid.
    /// </summary>
    /// <param name="plan">The plan to validate.</param>
    /// <param name="registeredToolNames">Names of tools registered on the agent.</param>
    /// <param name="registeredAgentNames">Names of agents registered in the system.</param>
    /// <param name="options">Execution options containing limits and allowlists.</param>
    /// <returns>A list of validation error messages. Empty if the plan is valid.</returns>
    public static List<string> Validate(
        AgentPlan plan,
        IEnumerable<string>? registeredToolNames = null,
        IEnumerable<string>? registeredAgentNames = null,
        PlanExecutionOptions? options = null)
    {
        options ??= new PlanExecutionOptions();
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(plan.Goal))
        {
            errors.Add("Plan must have a non-empty goal.");
        }

        if (plan.Steps is null || plan.Steps.Count == 0)
        {
            errors.Add("Plan must have at least one step.");
            return errors;
        }

        if (plan.Steps.Count > options.MaxSteps)
        {
            errors.Add($"Plan has {plan.Steps.Count} steps, exceeding the maximum of {options.MaxSteps}.");
        }

        // Build step index
        Dictionary<string, PlanStep> stepIndex = new(StringComparer.Ordinal);
        foreach (PlanStep step in plan.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id))
            {
                errors.Add("All steps must have a non-empty id.");
                continue;
            }

            if (!stepIndex.TryAdd(step.Id, step))
            {
                errors.Add($"Duplicate step id '{step.Id}'.");
            }
        }

        // Validate dependencies exist
        foreach (PlanStep step in plan.Steps)
        {
            if (step.DependsOn is null)
            {
                continue;
            }

            foreach (string dep in step.DependsOn)
            {
                if (!stepIndex.ContainsKey(dep))
                {
                    errors.Add($"Step '{step.Id}' depends on unknown step '{dep}'.");
                }
            }
        }

        // Cycle detection via topological sort (Kahn's algorithm)
        if (!TryTopologicalSort(plan.Steps, stepIndex, out List<string>? cycleErrors))
        {
            errors.AddRange(cycleErrors!);
        }

        // Reachability: every step must be reachable from at least one root
        HashSet<string> roots = new(
            plan.Steps
                .Where(s => s.DependsOn is null || s.DependsOn.Count == 0)
                .Select(s => s.Id));

        if (roots.Count == 0 && plan.Steps.Count > 0)
        {
            errors.Add("Plan has no root steps (steps with no dependencies). This indicates a cycle.");
        }

        // Validate actions
        HashSet<string>? toolSet = registeredToolNames is not null
            ? new HashSet<string>(registeredToolNames, StringComparer.Ordinal) : null;
        HashSet<string>? agentSet = registeredAgentNames is not null
            ? new HashSet<string>(registeredAgentNames, StringComparer.OrdinalIgnoreCase) : null;

        foreach (PlanStep step in plan.Steps)
        {
            ValidateAction(step, stepIndex, toolSet, agentSet, options, errors);
        }

        // Validate {step_id} references point to dependency-reachable steps
        foreach (PlanStep step in plan.Steps)
        {
            ValidateReferences(step, stepIndex, errors);
        }

        return errors;
    }

    private static void ValidateAction(
        PlanStep step,
        Dictionary<string, PlanStep> stepIndex,
        HashSet<string>? registeredTools,
        HashSet<string>? registeredAgents,
        PlanExecutionOptions options,
        List<string> errors)
    {
        if (step.Action is null)
        {
            errors.Add($"Step '{step.Id}' has no action.");
            return;
        }

        switch (step.Action)
        {
            case InvokeToolAction toolAction:
                if (string.IsNullOrWhiteSpace(toolAction.ToolName))
                {
                    errors.Add($"Step '{step.Id}': invoke_tool action must specify a toolName.");
                }
                else
                {
                    if (registeredTools?.Contains(toolAction.ToolName) == false)
                    {
                        errors.Add($"Step '{step.Id}': tool '{toolAction.ToolName}' is not registered.");
                    }

                    if (options.AllowedTools?.Contains(toolAction.ToolName) == false)
                    {
                        errors.Add($"Step '{step.Id}': tool '{toolAction.ToolName}' is not in the allowed tools list.");
                    }
                }

                break;

            case InvokeAgentAction agentAction:
                if (string.IsNullOrWhiteSpace(agentAction.AgentName))
                {
                    errors.Add($"Step '{step.Id}': invoke_agent action must specify an agentName.");
                }
                else
                {
                    if (registeredAgents?.Contains(agentAction.AgentName) == false)
                    {
                        errors.Add($"Step '{step.Id}': agent '{agentAction.AgentName}' is not registered.");
                    }

                    if (options.AllowedAgents?.Contains(agentAction.AgentName) == false)
                    {
                        errors.Add($"Step '{step.Id}': agent '{agentAction.AgentName}' is not in the allowed agents list.");
                    }
                }

                if (string.IsNullOrWhiteSpace(agentAction.Task))
                {
                    errors.Add($"Step '{step.Id}': invoke_agent action must specify a task.");
                }

                break;

            case SleepAction sleepAction:
                if (string.IsNullOrWhiteSpace(sleepAction.Duration))
                {
                    errors.Add($"Step '{step.Id}': sleep action must specify a duration.");
                }
                else
                {
                    try
                    {
                        XmlConvert.ToTimeSpan(sleepAction.Duration);
                    }
                    catch (FormatException)
                    {
                        errors.Add($"Step '{step.Id}': invalid ISO 8601 duration '{sleepAction.Duration}'.");
                    }
                }

                break;

            case PromptAction promptAction:
                if (string.IsNullOrWhiteSpace(promptAction.Prompt))
                {
                    errors.Add($"Step '{step.Id}': prompt action must specify a prompt.");
                }

                break;

            case WaitForInputAction waitAction:
                if (string.IsNullOrWhiteSpace(waitAction.Description))
                {
                    errors.Add($"Step '{step.Id}': wait_for_input action must specify a description.");
                }

                break;

            default:
                errors.Add($"Step '{step.Id}': unknown action type '{step.Action.GetType().Name}'.");
                break;
        }
    }

    private static void ValidateReferences(
        PlanStep step,
        Dictionary<string, PlanStep> stepIndex,
        List<string> errors)
    {
        // Collect all {step_id} references from action fields
        IEnumerable<string> references = ExtractReferences(step.Action);

        // Compute transitive dependencies for this step
        HashSet<string> reachable = GetTransitiveDependencies(step, stepIndex);

        foreach (string refId in references)
        {
            if (!stepIndex.ContainsKey(refId))
            {
                errors.Add($"Step '{step.Id}': references unknown step '{{{refId}}}'.");
            }
            else if (!reachable.Contains(refId))
            {
                errors.Add($"Step '{step.Id}': references '{{{refId}}}' which is not a dependency (direct or transitive).");
            }
        }
    }

    private static List<string> ExtractReferences(PlanStepAction action)
    {
        List<string> refs = [];

        switch (action)
        {
            case InvokeToolAction toolAction:
                if (toolAction.Arguments is not null)
                {
                    foreach (var arg in toolAction.Arguments.Values)
                    {
                        ExtractReferencesFromJson(arg.ToString(), refs);
                    }
                }

                break;
            case InvokeAgentAction agentAction:
                ExtractReferencesFromString(agentAction.Task, refs);
                break;
            case PromptAction promptAction:
                ExtractReferencesFromString(promptAction.Prompt, refs);
                break;
        }

        return refs;
    }

    private static void ExtractReferencesFromString(string? text, List<string> refs)
    {
        if (text is null)
        {
            return;
        }

        foreach (Match match in StepReferencePattern().Matches(text))
        {
            refs.Add(match.Groups[1].Value);
        }
    }

    private static void ExtractReferencesFromJson(string? json, List<string> refs)
    {
        // JSON argument values may contain {step_id} references as string values
        ExtractReferencesFromString(json, refs);
    }

    private static HashSet<string> GetTransitiveDependencies(
        PlanStep step,
        Dictionary<string, PlanStep> stepIndex)
    {
        HashSet<string> visited = [];
        Queue<string> queue = new();

        if (step.DependsOn is not null)
        {
            foreach (string dep in step.DependsOn)
            {
                queue.Enqueue(dep);
            }
        }

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (stepIndex.TryGetValue(current, out PlanStep? depStep) && depStep.DependsOn is not null)
            {
                foreach (string transitiveDep in depStep.DependsOn)
                {
                    queue.Enqueue(transitiveDep);
                }
            }
        }

        return visited;
    }

    private static bool TryTopologicalSort(
        List<PlanStep> steps,
        Dictionary<string, PlanStep> stepIndex,
        out List<string>? errors)
    {
        errors = null;
        Dictionary<string, int> inDegree = new(StringComparer.Ordinal);

        foreach (PlanStep step in steps)
        {
            inDegree.TryAdd(step.Id, 0);
        }

        foreach (PlanStep step in steps)
        {
            if (step.DependsOn is null)
            {
                continue;
            }

            foreach (string dep in step.DependsOn)
            {
                if (inDegree.TryGetValue(step.Id, out int current))
                {
                    inDegree[step.Id] = current + 1;
                }
            }
        }

        Queue<string> queue = new(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
        int processed = 0;

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            processed++;

            // Find steps that depend on current
            foreach (PlanStep step in steps)
            {
                if (step.DependsOn?.Contains(current) == true)
                {
                    inDegree[step.Id]--;
                    if (inDegree[step.Id] == 0)
                    {
                        queue.Enqueue(step.Id);
                    }
                }
            }
        }

        if (processed != steps.Count)
        {
            errors =
            [
                "Plan contains a cycle. Steps involved: " +
                string.Join(", ", inDegree.Where(kvp => kvp.Value > 0).Select(kvp => $"'{kvp.Key}'")),
            ];
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.Compiled)]
    private static partial Regex StepReferencePattern();
}
