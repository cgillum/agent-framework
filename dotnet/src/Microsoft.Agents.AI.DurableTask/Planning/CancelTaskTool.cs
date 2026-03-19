// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// A built-in tool that cancels a running plan task by terminating its orchestration.
/// </summary>
public sealed class CancelTaskTool
{
    private readonly AITool _tool;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelTaskTool"/> class.
    /// </summary>
    public CancelTaskTool()
    {
        this._tool = AIFunctionFactory.Create(
            CancelTaskAsync,
            new AIFunctionFactoryOptions
            {
                Name = "cancel_task",
                Description = "Cancel a running plan task.",
            });
    }

    /// <summary>
    /// Gets the <see cref="AITool"/> that can be registered on an agent.
    /// </summary>
    public AITool Tool => this._tool;

    /// <summary>
    /// Converts a <see cref="CancelTaskTool"/> to an <see cref="AITool"/> for use in agent tool lists.
    /// </summary>
    /// <param name="tool">The <see cref="CancelTaskTool"/> to convert.</param>
    public static implicit operator AITool(CancelTaskTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool._tool;
    }

    [Description("Cancel a running plan task.")]
    private static async Task<string> CancelTaskAsync(
        [Description("The task ID to cancel")] string taskId)
    {
        DurableAgentContext agentContext = DurableAgentContext.Current;

        await agentContext.Client.TerminateInstanceAsync(taskId, "Cancelled by agent");

        // Remove from the tracked plan tasks
        PlanTasksContext.Current?.Remove(taskId);

        return $"Task '{taskId}' has been cancelled.";
    }
}
