// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// A built-in tool that provides input to a plan task step waiting for human input.
/// </summary>
public sealed class SendTaskInputTool
{
    private readonly AITool _tool;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendTaskInputTool"/> class.
    /// </summary>
    public SendTaskInputTool()
    {
        this._tool = AIFunctionFactory.Create(
            SendTaskInputAsync,
            new AIFunctionFactoryOptions
            {
                Name = "send_task_input",
                Description = "Provide input for a plan task step that is waiting for human input.",
            });
    }

    /// <summary>
    /// Gets the <see cref="AITool"/> that can be registered on an agent.
    /// </summary>
    public AITool Tool => this._tool;

    /// <summary>
    /// Converts a <see cref="SendTaskInputTool"/> to an <see cref="AITool"/> for use in agent tool lists.
    /// </summary>
    /// <param name="tool">The <see cref="SendTaskInputTool"/> to convert.</param>
    public static implicit operator AITool(SendTaskInputTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool._tool;
    }

    [Description("Provide input for a plan task step that is waiting for human input.")]
    private static async Task<string> SendTaskInputAsync(
        [Description("The task ID to send input to")] string taskId,
        [Description("The input name identifying which step to send input to")] string inputName,
        [Description("The input value to provide")] string input)
    {
        DurableAgentContext agentContext = DurableAgentContext.Current;

        await agentContext.RaiseOrchestrationEventAsync(
            taskId,
            $"PlanStepInput_{inputName}",
            input);

        return $"Input '{inputName}' sent to task '{taskId}'.";
    }
}
