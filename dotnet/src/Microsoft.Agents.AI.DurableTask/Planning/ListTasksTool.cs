// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// A built-in tool that lists all plan tasks for the current agent session.
/// </summary>
public sealed class ListTasksTool
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ListTasksTool"/> class.
    /// </summary>
    public ListTasksTool()
    {
        this.Tool = AIFunctionFactory.Create(
            ListTasks,
            new AIFunctionFactoryOptions
            {
                Name = "list_tasks",
                Description = "List all plan tasks for this session.",
            });
    }

    /// <summary>
    /// Gets the <see cref="AITool"/> that can be registered on an agent.
    /// </summary>
    public AITool Tool { get; }

    /// <summary>
    /// Converts a <see cref="ListTasksTool"/> to an <see cref="AITool"/> for use in agent tool lists.
    /// </summary>
    /// <param name="tool">The <see cref="ListTasksTool"/> to convert.</param>
    public static implicit operator AITool(ListTasksTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool.Tool;
    }

    [Description("List all plan tasks for this session.")]
    private static string ListTasks()
    {
        Dictionary<string, PlanTaskRecord>? planTasks = PlanTasksContext.Current;

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("tasks");

            if (planTasks is not null)
            {
                foreach (PlanTaskRecord record in planTasks.Values)
                {
                    writer.WriteStartObject();
                    writer.WriteString("taskId", record.TaskId);
                    writer.WriteString("planGoal", record.PlanGoal);
                    writer.WriteNumber("stepCount", record.StepCount);
                    writer.WriteString("createdAt", record.CreatedAt.ToString("O"));
                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
