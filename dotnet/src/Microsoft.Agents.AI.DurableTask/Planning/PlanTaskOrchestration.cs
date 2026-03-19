// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Durable Task orchestration that executes a plan DAG. Steps are scheduled in
/// dependency order with maximum parallelism: all steps whose dependencies have
/// completed are fanned-out concurrently, and the orchestration fans-in before
/// starting the next wave.
/// </summary>
public class PlanTaskOrchestration : TaskOrchestrator<PlanTaskInput, PlanTaskResult>
{
    /// <inheritdoc/>
    public override async Task<PlanTaskResult> RunAsync(
        TaskOrchestrationContext context,
        PlanTaskInput input)
    {
        ILogger logger = context.CreateReplaySafeLogger<PlanTaskOrchestration>();
        string planId = context.InstanceId;
        AgentPlan plan = input.Plan;
        PlanExecutionOptions options = input.Options ?? new PlanExecutionOptions();

        // Validate the plan before execution
        List<string> validationErrors = PlanValidator.Validate(plan, options: options);
        if (validationErrors.Count > 0)
        {
            logger.LogPlanValidationFailed(planId, validationErrors.Count);
            return new PlanTaskResult
            {
                Status = PlanTaskStatus.Failed,
                Error = "Plan validation failed: " + string.Join("; ", validationErrors),
            };
        }

        logger.LogPlanStarted(planId, plan.Goal, plan.Steps.Count);
        DateTimeOffset startTime = context.CurrentUtcDateTime;

        // Build step index and track results
        Dictionary<string, PlanStep> stepIndex = new(StringComparer.Ordinal);
        foreach (PlanStep step in plan.Steps)
        {
            stepIndex[step.Id] = step;
        }

        Dictionary<string, PlanStepResult> completedResults = new(StringComparer.Ordinal);
        int replanAttempts = 0;

        // Execute the DAG wave-by-wave
        while (completedResults.Count < plan.Steps.Count)
        {
            // Find steps whose dependencies are all satisfied
            List<PlanStep> readySteps = [];
            foreach (PlanStep step in plan.Steps)
            {
                if (completedResults.ContainsKey(step.Id))
                {
                    continue;
                }

                bool allDependenciesMet = true;
                if (step.DependsOn is not null)
                {
                    foreach (string dep in step.DependsOn)
                    {
                        if (!completedResults.TryGetValue(dep, out PlanStepResult? depResult) ||
                            depResult.Status != PlanStepStatus.Completed)
                        {
                            allDependenciesMet = false;
                            break;
                        }
                    }
                }

                if (allDependenciesMet)
                {
                    readySteps.Add(step);
                }
            }

            if (readySteps.Count == 0)
            {
                // No steps are ready — remaining steps have failed dependencies
                const string error = "No steps are ready to execute; remaining steps have unsatisfied dependencies.";
                logger.LogPlanFailed(planId, error);
                return new PlanTaskResult
                {
                    Status = PlanTaskStatus.Failed,
                    StepResults = completedResults,
                    Error = error,
                };
            }

            // Publish progress
            context.SetCustomStatus(new TaskProgress
            {
                Status = PlanTaskStatus.Working,
                CompletedSteps = [.. completedResults.Keys],
                CurrentSteps = readySteps.ConvertAll(s => s.Id),
            });

            // Fan-out: execute all ready steps in parallel
            List<Task<PlanStepResult>> stepTasks = [];
            foreach (PlanStep step in readySteps)
            {
                stepTasks.Add(ExecuteStepAsync(context, logger, planId, step, completedResults, input, options));
            }

            PlanStepResult[] waveResults = await Task.WhenAll(stepTasks);

            // Fan-in: collect results
            foreach (PlanStepResult result in waveResults)
            {
                completedResults[result.StepId] = result;

                if (result.Status == PlanStepStatus.Failed)
                {
                    if (options.EnableReplanning && replanAttempts < options.MaxReplanAttempts)
                    {
                        replanAttempts++;
                        logger.LogPlanReplanningTriggered(planId, result.StepId, replanAttempts);

                        context.SetCustomStatus(new TaskProgress
                        {
                            Status = PlanTaskStatus.Replanning,
                            CompletedSteps = [.. completedResults.Keys],
                            StatusMessage = $"Re-planning after step '{result.StepId}' failed (attempt {replanAttempts})",
                        });

                        AgentPlan? revisedPlan = await TryReplanAsync(
                            context, logger, planId, plan, result, completedResults, input);

                        if (revisedPlan is not null)
                        {
                            // Merge: keep completed results, replace the plan with the revised one
                            plan = MergePlan(plan, revisedPlan, completedResults);

                            // Rebuild step index for the merged plan
                            stepIndex.Clear();
                            foreach (PlanStep step in plan.Steps)
                            {
                                stepIndex[step.Id] = step;
                            }

                            // Continue the outer loop with the merged plan
                            break;
                        }

                        // Re-planning failed — fall through to the normal failure path
                    }

                    logger.LogPlanFailed(planId, result.Error ?? "Unknown error");
                    return PlanTaskResult.Failed(result.StepId, result.Error ?? "Unknown error");
                }
            }
        }

        // All steps completed successfully
        long totalDurationMs = (long)(context.CurrentUtcDateTime - startTime).TotalMilliseconds;
        logger.LogPlanCompleted(planId, plan.Steps.Count, totalDurationMs);

        return PlanTaskResult.Completed(completedResults);
    }

    /// <summary>
    /// Attempts to re-plan by calling the agent with context about completed steps,
    /// the failed step, and remaining pending steps.
    /// </summary>
    /// <returns>A revised <see cref="AgentPlan"/> or <see langword="null"/> if re-planning fails.</returns>
    private static async Task<AgentPlan?> TryReplanAsync(
        TaskOrchestrationContext context,
        ILogger logger,
        string planId,
        AgentPlan originalPlan,
        PlanStepResult failedResult,
        Dictionary<string, PlanStepResult> completedResults,
        PlanTaskInput input)
    {
        try
        {
            string replanPrompt = BuildReplanPrompt(
                originalPlan, failedResult, completedResults);

            AgentSessionId sessionId = context.NewAgentSessionId(input.AgentName);
            RunRequest request = new(replanPrompt, ChatRole.User);

            AgentResponse response = await context.Entities.CallEntityAsync<AgentResponse>(
                sessionId,
                nameof(AgentEntity.Run),
                request);

            if (string.IsNullOrWhiteSpace(response.Text))
            {
                return null;
            }

            AgentPlan? revisedPlan = (AgentPlan?)JsonSerializer.Deserialize(
                response.Text,
                DurableAgentJsonUtilities.DefaultOptions.GetTypeInfo(typeof(AgentPlan)));

            if (revisedPlan is null || revisedPlan.Steps is null || revisedPlan.Steps.Count == 0)
            {
                return null;
            }

            return revisedPlan;
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            logger.LogPlanFailed(planId, $"Re-planning failed: {ex.Message}");
            return null;
        }
    }

    private static string BuildReplanPrompt(
        AgentPlan originalPlan,
        PlanStepResult failedResult,
        Dictionary<string, PlanStepResult> completedResults)
    {
        StringBuilder sb = new();
        sb.AppendLine("A plan step has failed and the plan needs to be revised.");
        sb.AppendLine();
        sb.Append("## Original Goal\n\n").AppendLine(originalPlan.Goal);
        sb.AppendLine();

        // Completed steps
        List<PlanStepResult> completed = completedResults.Values
            .Where(r => r.Status == PlanStepStatus.Completed)
            .ToList();
        if (completed.Count > 0)
        {
            sb.AppendLine("## Completed Steps");
            sb.AppendLine();
            foreach (PlanStepResult result in completed)
            {
                sb.Append("- **").Append(result.StepId).Append("**: ");
                sb.AppendLine(result.Result ?? "(no result)");
            }

            sb.AppendLine();
        }

        // Failed step
        sb.AppendLine("## Failed Step");
        sb.AppendLine();
        sb.Append("- **Step ID**: ").AppendLine(failedResult.StepId);
        sb.Append("- **Error**: ").AppendLine(failedResult.Error ?? "Unknown error");
        sb.AppendLine();

        // Remaining pending steps
        HashSet<string> processedIds = new(completedResults.Keys, StringComparer.Ordinal);
        List<PlanStep> pendingSteps = originalPlan.Steps
            .Where(s => !processedIds.Contains(s.Id))
            .ToList();
        if (pendingSteps.Count > 0)
        {
            sb.AppendLine("## Remaining Pending Steps");
            sb.AppendLine();
            foreach (PlanStep step in pendingSteps)
            {
                sb.Append("- **").Append(step.Id).Append("**: ").AppendLine(step.Description);
            }

            sb.AppendLine();
        }

        sb.AppendLine("Please produce a revised plan (as a JSON AgentPlan) that achieves the original goal,");
        sb.AppendLine("taking into account the completed step results and the failure. Do NOT repeat completed steps.");
        sb.AppendLine("Output ONLY a valid JSON object matching the AgentPlan schema — no markdown fences, no commentary.");
        return sb.ToString();
    }

    /// <summary>
    /// Merges a revised plan with the original plan, keeping all completed step results
    /// and replacing pending steps with the revised plan's steps.
    /// </summary>
    private static AgentPlan MergePlan(
        AgentPlan originalPlan,
        AgentPlan revisedPlan,
        Dictionary<string, PlanStepResult> completedResults)
    {
        // Keep completed steps from the original plan
        List<PlanStep> mergedSteps = originalPlan.Steps
            .Where(s => completedResults.ContainsKey(s.Id) &&
                        completedResults[s.Id].Status == PlanStepStatus.Completed)
            .ToList();

        // Add all new steps from the revised plan (avoiding duplicates with completed steps)
        HashSet<string> completedIds = new(completedResults
            .Where(kvp => kvp.Value.Status == PlanStepStatus.Completed)
            .Select(kvp => kvp.Key), StringComparer.Ordinal);

        foreach (PlanStep step in revisedPlan.Steps)
        {
            if (!completedIds.Contains(step.Id))
            {
                mergedSteps.Add(step);
            }
        }

        return new AgentPlan
        {
            Goal = revisedPlan.Goal ?? originalPlan.Goal,
            Steps = mergedSteps,
        };
    }

    private static async Task<PlanStepResult> ExecuteStepAsync(
        TaskOrchestrationContext context,
        ILogger logger,
        string planId,
        PlanStep step,
        IReadOnlyDictionary<string, PlanStepResult> completedResults,
        PlanTaskInput input,
        PlanExecutionOptions options)
    {
        string actionType = step.Action switch
        {
            InvokeToolAction => "invoke_tool",
            InvokeAgentAction => "invoke_agent",
            SleepAction => "sleep",
            PromptAction => "prompt",
            WaitForInputAction => "wait_for_input",
            _ => "unknown",
        };

        logger.LogPlanStepStarted(planId, step.Id, actionType);
        DateTimeOffset stepStart = context.CurrentUtcDateTime;

        try
        {
            // Resolve {step_id} references in action fields
            PlanStepAction resolvedAction = PlanReferenceResolver.ResolveReferences(
                step.Action, completedResults);

            string? resultText = await ExecuteActionAsync(
                context, logger, planId, step.Id, resolvedAction, input, options);

            long durationMs = (long)(context.CurrentUtcDateTime - stepStart).TotalMilliseconds;
            logger.LogPlanStepCompleted(planId, step.Id, durationMs);

            return new PlanStepResult
            {
                StepId = step.Id,
                Status = PlanStepStatus.Completed,
                Result = resultText,
                CompletedAt = context.CurrentUtcDateTime,
            };
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            long durationMs = (long)(context.CurrentUtcDateTime - stepStart).TotalMilliseconds;
            string error = ex.Message;
            logger.LogPlanStepFailed(planId, step.Id, error);

            return new PlanStepResult
            {
                StepId = step.Id,
                Status = PlanStepStatus.Failed,
                Error = error,
                CompletedAt = context.CurrentUtcDateTime,
            };
        }
    }

    private static async Task<string?> ExecuteActionAsync(
        TaskOrchestrationContext context,
        ILogger logger,
        string planId,
        string stepId,
        PlanStepAction action,
        PlanTaskInput input,
        PlanExecutionOptions options)
    {
        switch (action)
        {
            case InvokeToolAction toolAction:
                return await ExecuteToolActionAsync(context, input, toolAction, options);

            case InvokeAgentAction agentAction:
                return await ExecuteAgentActionAsync(context, input, agentAction);

            case SleepAction sleepAction:
                TimeSpan duration = XmlConvert.ToTimeSpan(sleepAction.Duration);
                await context.CreateTimer(duration, CancellationToken.None);
                return $"Slept for {duration}.";

            case PromptAction promptAction:
                return await ExecutePromptActionAsync(context, input, promptAction);

            case WaitForInputAction waitAction:
                return await ExecuteWaitForInputAsync(
                    context, logger, planId, stepId, waitAction, options);

            default:
                throw new InvalidOperationException($"Unknown action type: {action.GetType().Name}");
        }
    }

    /// <summary>
    /// Executes an <c>invoke_tool</c> step by calling the planning agent entity with a
    /// message requesting tool execution. The agent's LLM tool-calling capability handles
    /// the actual tool invocation within the entity context where tools are registered.
    /// </summary>
    /// <remarks>
    /// This approach routes tool calls through the agent entity to ensure tools execute
    /// in the correct context. A future optimization could use direct activity-based tool
    /// invocation to avoid the LLM roundtrip.
    /// </remarks>
    // TODO: Consider direct activity-based tool invocation to avoid LLM roundtrip overhead.
    private static async Task<string?> ExecuteToolActionAsync(
        TaskOrchestrationContext context,
        PlanTaskInput input,
        InvokeToolAction toolAction,
        PlanExecutionOptions options)
    {
        string toolPrompt = $"Execute the tool '{toolAction.ToolName}'";
        if (toolAction.Arguments is { Count: > 0 })
        {
            string argsJson = System.Text.Json.JsonSerializer.Serialize(
                toolAction.Arguments,
                DurableAgentJsonUtilities.DefaultOptions.GetTypeInfo(
                    typeof(Dictionary<string, System.Text.Json.JsonElement>)));
            toolPrompt += $" with these arguments: {argsJson}";
        }

        toolPrompt += ". Return only the tool result.";

        AgentSessionId sessionId = context.NewAgentSessionId(input.AgentName);
        RunRequest request = new(toolPrompt)
        {
            EnableToolNames = [toolAction.ToolName],
        };

        AgentResponse response = await context.Entities.CallEntityAsync<AgentResponse>(
            sessionId,
            nameof(AgentEntity.Run),
            request);

        return response.Text;
    }

    private static async Task<string?> ExecuteAgentActionAsync(
        TaskOrchestrationContext context,
        PlanTaskInput input,
        InvokeAgentAction agentAction)
    {
        AgentSessionId sessionId = context.NewAgentSessionId(agentAction.AgentName);
        RunRequest request = new(agentAction.Task);

        AgentResponse response = await context.Entities.CallEntityAsync<AgentResponse>(
            sessionId,
            nameof(AgentEntity.Run),
            request);

        return response.Text;
    }

    private static async Task<string?> ExecutePromptActionAsync(
        TaskOrchestrationContext context,
        PlanTaskInput input,
        PromptAction promptAction)
    {
        AgentSessionId sessionId = context.NewAgentSessionId(input.AgentName);
        RunRequest request = new(promptAction.Prompt);

        AgentResponse response = await context.Entities.CallEntityAsync<AgentResponse>(
            sessionId,
            nameof(AgentEntity.Run),
            request);

        return response.Text;
    }

    private static async Task<string?> ExecuteWaitForInputAsync(
        TaskOrchestrationContext context,
        ILogger logger,
        string planId,
        string stepId,
        WaitForInputAction waitAction,
        PlanExecutionOptions options)
    {
        string inputName = waitAction.InputName ?? stepId;

        // Publish status indicating human input is required
        context.SetCustomStatus(new TaskProgress
        {
            Status = PlanTaskStatus.InputRequired,
            StatusMessage = waitAction.Description,
            PendingInputs =
            [
                new PendingInput
                {
                    StepId = stepId,
                    InputName = inputName,
                    Description = waitAction.Description,
                },
            ],
        });

        logger.LogPlanWaitingForInput(planId, stepId, inputName);

        string eventName = $"PlanStepInput_{inputName}";

        using CancellationTokenSource timeoutCts = new();
        Task<string> inputTask = context.WaitForExternalEvent<string>(eventName);
        Task timeoutTask = context.CreateTimer(options.WaitForInputTimeout, timeoutCts.Token);

        Task completedTask = await Task.WhenAny(inputTask, timeoutTask);
        if (completedTask == timeoutTask)
        {
            throw new TimeoutException(
                $"Timed out waiting for input '{inputName}' after {options.WaitForInputTimeout}.");
        }

        // Cancel the timeout timer since input was received
        timeoutCts.Cancel();

        string result = await inputTask;
        logger.LogPlanInputReceived(planId, stepId, inputName);
        return result;
    }
}

/// <summary>
/// Extension methods for exception handling in orchestrations.
/// </summary>
internal static class ExceptionExtensions
{
    /// <summary>
    /// Determines whether an exception is fatal and should not be caught.
    /// </summary>
    public static bool IsFatal(this Exception ex) =>
        ex is OutOfMemoryException or StackOverflowException or ThreadAbortException;
}
