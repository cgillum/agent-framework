// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// A built-in tool that retrieves the detailed status of a specific plan task,
/// including progress, pending inputs, and results.
/// </summary>
public sealed class GetTaskStatusTool
{
    private readonly AITool _tool;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTaskStatusTool"/> class.
    /// </summary>
    public GetTaskStatusTool()
    {
        this._tool = AIFunctionFactory.Create(
            GetTaskStatusAsync,
            new AIFunctionFactoryOptions
            {
                Name = "get_task_status",
                Description = "Get detailed status of a specific plan task.",
            });
    }

    /// <summary>
    /// Gets the <see cref="AITool"/> that can be registered on an agent.
    /// </summary>
    public AITool Tool => this._tool;

    /// <summary>
    /// Converts a <see cref="GetTaskStatusTool"/> to an <see cref="AITool"/> for use in agent tool lists.
    /// </summary>
    /// <param name="tool">The <see cref="GetTaskStatusTool"/> to convert.</param>
    public static implicit operator AITool(GetTaskStatusTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool._tool;
    }

    [Description("Get detailed status of a specific plan task.")]
    private static async Task<string> GetTaskStatusAsync(
        [Description("The task ID to get status for")] string taskId)
    {
        DurableAgentContext agentContext = DurableAgentContext.Current;

        OrchestrationMetadata? metadata = await agentContext.GetOrchestrationStatusAsync(
            taskId, includeDetails: true);

        if (metadata is null)
        {
            return $"Task '{taskId}' not found.";
        }

        // Parse custom status if available
        TaskProgress? progress = null;
        if (metadata.SerializedCustomStatus is not null)
        {
            try
            {
                progress = (TaskProgress?)JsonSerializer.Deserialize(
                    metadata.SerializedCustomStatus,
                    DurableAgentJsonUtilities.DefaultOptions.GetTypeInfo(typeof(TaskProgress)));
            }
            catch (JsonException)
            {
                // Custom status could not be parsed; proceed without it.
            }
        }

        // Parse output if completed
        PlanTaskResult? result = null;
        if (metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed &&
            metadata.SerializedOutput is not null)
        {
            try
            {
                result = (PlanTaskResult?)JsonSerializer.Deserialize(
                    metadata.SerializedOutput,
                    DurableAgentJsonUtilities.DefaultOptions.GetTypeInfo(typeof(PlanTaskResult)));
            }
            catch (JsonException)
            {
                // Output could not be parsed; proceed without it.
            }
        }

        return WriteStatusResponse(taskId, metadata, progress, result);
    }

    private static string WriteStatusResponse(
        string taskId,
        OrchestrationMetadata metadata,
        TaskProgress? progress,
        PlanTaskResult? result)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("taskId", taskId);
            writer.WriteString("runtimeStatus", metadata.RuntimeStatus.ToString());

            if (progress is not null)
            {
                writer.WriteString("status", progress.Status.ToString());

                if (progress.StatusMessage is not null)
                {
                    writer.WriteString("statusMessage", progress.StatusMessage);
                }

                writer.WriteStartArray("completedSteps");
                foreach (string step in progress.CompletedSteps)
                {
                    writer.WriteStringValue(step);
                }

                writer.WriteEndArray();

                writer.WriteStartArray("currentSteps");
                foreach (string step in progress.CurrentSteps)
                {
                    writer.WriteStringValue(step);
                }

                writer.WriteEndArray();

                if (progress.PendingInputs is not null)
                {
                    writer.WriteStartArray("pendingInputs");
                    foreach (PendingInput input in progress.PendingInputs)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("stepId", input.StepId);
                        writer.WriteString("inputName", input.InputName);
                        writer.WriteString("description", input.Description);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }
            }

            if (result is not null)
            {
                writer.WriteString("resultStatus", result.Status.ToString());

                if (result.Error is not null)
                {
                    writer.WriteString("error", result.Error);
                }
            }
            else if (metadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
            {
                string errorMessage = metadata.FailureDetails?.ErrorMessage ?? "Unknown error";
                writer.WriteString("error", errorMessage);
            }

            bool isRunning = metadata.RuntimeStatus is
                OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending;

            if (isRunning)
            {
                writer.WriteNumber("pollInterval", 5000);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
