// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Resolves <c>{step_id}</c> template references in plan step action fields,
/// replacing them with results from completed steps.
/// </summary>
public static partial class PlanReferenceResolver
{
    /// <summary>
    /// Resolves all <c>{step_id}</c> references in the given action's fields,
    /// producing a new action with references replaced by step results.
    /// </summary>
    /// <param name="action">The action containing references to resolve.</param>
    /// <param name="completedResults">Results of completed steps, keyed by step ID.</param>
    /// <returns>A new action with all references resolved.</returns>
    public static PlanStepAction ResolveReferences(
        PlanStepAction action,
        IReadOnlyDictionary<string, PlanStepResult> completedResults)
    {
        return action switch
        {
            InvokeToolAction toolAction => new InvokeToolAction
            {
                ToolName = toolAction.ToolName,
                Arguments = ResolveJsonArguments(toolAction.Arguments, completedResults),
            },
            InvokeAgentAction agentAction => new InvokeAgentAction
            {
                AgentName = agentAction.AgentName,
                Task = ResolveString(agentAction.Task, completedResults),
            },
            PromptAction promptAction => new PromptAction
            {
                Prompt = ResolveString(promptAction.Prompt, completedResults),
            },
            // SleepAction and WaitForInputAction have no resolvable fields
            _ => action,
        };
    }

    /// <summary>
    /// Resolves <c>{step_id}</c> references in a string, replacing each with the
    /// text result of the referenced step.
    /// </summary>
    public static string ResolveString(
        string template,
        IReadOnlyDictionary<string, PlanStepResult> completedResults)
    {
        return StepReferencePattern().Replace(template, match =>
        {
            string stepId = match.Groups[1].Value;
            if (completedResults.TryGetValue(stepId, out PlanStepResult? result))
            {
                return result.Result ?? result.JsonResult?.ToString() ?? string.Empty;
            }

            // Leave unresolved references as-is (validator should have caught this)
            return match.Value;
        });
    }

    /// <summary>
    /// Resolves <c>{step_id}</c> references in JSON arguments. When a JSON value
    /// is exactly <c>"{step_id}"</c>, it is structurally replaced with the step's
    /// JSON result (object embedding, not string escaping).
    /// </summary>
    private static Dictionary<string, JsonElement>? ResolveJsonArguments(
        Dictionary<string, JsonElement>? arguments,
        IReadOnlyDictionary<string, PlanStepResult> completedResults)
    {
        if (arguments is null)
        {
            return null;
        }

        Dictionary<string, JsonElement> resolved = new(arguments.Count);
        foreach (var (key, value) in arguments)
        {
            resolved[key] = ResolveJsonValue(value, completedResults);
        }

        return resolved;
    }

    private static JsonElement ResolveJsonValue(
        JsonElement value,
        IReadOnlyDictionary<string, PlanStepResult> completedResults)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string? text = value.GetString();
            if (text is not null)
            {
                // Check for exact match: "{step_id}" → structural JSON embedding
                Match exactMatch = ExactStepReferencePattern().Match(text);
                if (exactMatch.Success)
                {
                    string stepId = exactMatch.Groups[1].Value;
                    if (completedResults.TryGetValue(stepId, out PlanStepResult? result))
                    {
                        // Structural embedding: return the JSON result directly
                        if (result.JsonResult.HasValue)
                        {
                            return result.JsonResult.Value;
                        }

                        // Fall back to wrapping the text result as a JSON string
                        return CreateJsonString(result.Result ?? string.Empty);
                    }
                }

                // Partial references in a string: resolve as string replacement
                string resolvedText = ResolveString(text, completedResults);
                if (resolvedText != text)
                {
                    return CreateJsonString(resolvedText);
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            // Recursively resolve object properties
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                foreach (JsonProperty prop in value.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    ResolveJsonValue(prop.Value, completedResults).WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            using var doc = JsonDocument.Parse(ms.ToArray());
            return doc.RootElement.Clone();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            // Recursively resolve array elements
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    ResolveJsonValue(item, completedResults).WriteTo(writer);
                }

                writer.WriteEndArray();
            }

            using var doc = JsonDocument.Parse(ms.ToArray());
            return doc.RootElement.Clone();
        }

        return value;
    }

    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.Compiled)]
    private static partial Regex StepReferencePattern();

    [GeneratedRegex(@"^\{(\w+)\}$", RegexOptions.Compiled)]
    private static partial Regex ExactStepReferencePattern();

    private static JsonElement CreateJsonString(string value)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStringValue(value);
        }

        using var doc = JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.Clone();
    }
}
