// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Extension methods on <see cref="TaskOrchestrationContext"/> for developer-driven
/// plan generation and execution within orchestrations.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class PlanOrchestrationExtensions
{
    /// <summary>
    /// Generates an <see cref="AgentPlan"/> by calling an agent with a system prompt
    /// that instructs it to decompose the given task into a structured plan.
    /// </summary>
    /// <param name="context">The orchestration context.</param>
    /// <param name="agent">The durable agent to use for plan generation.</param>
    /// <param name="task">The natural-language task description to plan for.</param>
    /// <param name="session">The agent session to use for the generation call.</param>
    /// <param name="options">Optional plan generation options.</param>
    /// <returns>A validated <see cref="AgentPlan"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the agent fails to produce a valid plan after all retry attempts.
    /// </exception>
    public static async Task<AgentPlan> GeneratePlanAsync(
        this TaskOrchestrationContext context,
        DurableAIAgent agent,
        string task,
        DurableAgentSession session,
        PlanGenerationOptions? options = null)
    {
        _ = context; // Extension target — used by callers for discoverability
        options ??= new PlanGenerationOptions();
        int maxRetries = options.MaxValidationRetries;

        // Build the system prompt
        string systemPrompt = options.SystemPrompt
            ?? PlanSystemPromptBuilder.BuildSystemPrompt(
                availableTools: null,
                availableAgentNames: null,
                options: null,
                additionalInstructions: options.AdditionalInstructions);

        List<ChatMessage> messages =
        [
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, task),
        ];

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            AgentResponse<AgentPlan> response = await agent.RunAsync<AgentPlan>(
                messages,
                session,
                serializerOptions: DurableAgentJsonUtilities.DefaultOptions);

            AgentPlan plan = response.Result;

            List<string> validationErrors = PlanValidator.Validate(plan);
            if (validationErrors.Count == 0)
            {
                return plan;
            }

            if (attempt < maxRetries)
            {
                // Append validation feedback so the agent can correct the plan
                string feedback = "The plan you produced has validation errors. Please fix them and try again:\n"
                    + string.Join("\n", validationErrors.ConvertAll(e => $"- {e}"));
                messages.Add(new ChatMessage(ChatRole.Assistant, response.Text));
                messages.Add(new ChatMessage(ChatRole.User, feedback));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Plan generation failed after {maxRetries + 1} attempt(s). " +
                    $"Validation errors: {string.Join("; ", validationErrors)}");
            }
        }

        // Unreachable, but satisfies the compiler
        throw new InvalidOperationException("Plan generation failed unexpectedly.");
    }

    /// <summary>
    /// Executes a previously generated <see cref="AgentPlan"/> by scheduling a
    /// <see cref="PlanTaskOrchestration"/> as a sub-orchestration.
    /// </summary>
    /// <param name="context">The orchestration context.</param>
    /// <param name="plan">The plan to execute.</param>
    /// <param name="agent">The durable agent that owns the plan.</param>
    /// <param name="session">The agent session associated with the plan.</param>
    /// <param name="options">Optional plan execution options.</param>
    /// <returns>The result of executing the plan.</returns>
    public static Task<PlanTaskResult> ExecutePlanAsync(
        this TaskOrchestrationContext context,
        AgentPlan plan,
        DurableAIAgent agent,
        DurableAgentSession session,
        PlanExecutionOptions? options = null)
    {
        PlanTaskInput input = new()
        {
            Plan = plan,
            AgentName = session.SessionId.Name,
            Options = options,
        };

        return context.CallSubOrchestratorAsync<PlanTaskResult>(
            nameof(PlanTaskOrchestration),
            input);
    }

    /// <summary>
    /// Generates a plan from a natural-language task and then executes it, combining
    /// <see cref="GeneratePlanAsync"/> and <see cref="ExecutePlanAsync"/> into a single call.
    /// </summary>
    /// <param name="context">The orchestration context.</param>
    /// <param name="agent">The durable agent to use for planning and execution.</param>
    /// <param name="task">The natural-language task description.</param>
    /// <param name="session">The agent session to use.</param>
    /// <param name="generationOptions">Optional plan generation options.</param>
    /// <param name="executionOptions">Optional plan execution options.</param>
    /// <returns>The result of executing the generated plan.</returns>
    public static async Task<PlanTaskResult> PlanAndExecuteAsync(
        this TaskOrchestrationContext context,
        DurableAIAgent agent,
        string task,
        DurableAgentSession session,
        PlanGenerationOptions? generationOptions = null,
        PlanExecutionOptions? executionOptions = null)
    {
        AgentPlan plan = await context.GeneratePlanAsync(agent, task, session, generationOptions);
        return await context.ExecutePlanAsync(plan, agent, session, executionOptions);
    }
}
