// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.DurableTask.Planning;

/// <summary>
/// Builds the system prompt used by the plan generation LLM call, describing
/// available tools, agents, schema, and constraints.
/// </summary>
public static class PlanSystemPromptBuilder
{
    /// <summary>
    /// Builds a system prompt that instructs an LLM to produce a valid <see cref="AgentPlan"/> JSON document.
    /// </summary>
    /// <param name="availableTools">Tools the plan steps may invoke.</param>
    /// <param name="availableAgentNames">Agent names the plan steps may delegate to.</param>
    /// <param name="options">Execution constraints such as max steps.</param>
    /// <param name="additionalInstructions">Extra instructions appended to the prompt.</param>
    /// <returns>The assembled system prompt string.</returns>
    public static string BuildSystemPrompt(
        IEnumerable<AITool>? availableTools,
        IEnumerable<string>? availableAgentNames,
        PlanExecutionOptions? options,
        string? additionalInstructions)
    {
        options ??= new PlanExecutionOptions();

        StringBuilder sb = new();

        // Role
        sb.AppendLine("You are a planning agent. Your job is to decompose a user's request into a structured plan.");
        sb.AppendLine("Output ONLY a valid JSON object matching the AgentPlan schema below — no markdown fences, no commentary.");
        sb.AppendLine();

        // Schema
        sb.AppendLine("## AgentPlan JSON Schema");
        sb.AppendLine();
        sb.AppendLine("{");
        sb.AppendLine("  \"goal\": \"<string: high-level description of what the plan accomplishes>\",");
        sb.AppendLine("  \"steps\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"id\": \"<string: unique step identifier>\",");
        sb.AppendLine("      \"description\": \"<string: what this step does>\",");
        sb.AppendLine("      \"action\": { <action object — see step types below> },");
        sb.AppendLine("      \"dependsOn\": [\"<step_id>\", ...]  // optional, omit if no dependencies");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();

        // Step types
        sb.AppendLine("## Step Types");
        sb.AppendLine();
        sb.AppendLine("### invoke_tool");
        sb.AppendLine("Execute a registered tool.");
        sb.AppendLine("{ \"type\": \"invoke_tool\", \"toolName\": \"<name>\", \"arguments\": { \"<param>\": <value>, ... } }");
        sb.AppendLine();
        sb.AppendLine("### invoke_agent");
        sb.AppendLine("Delegate work to a sub-agent.");
        sb.AppendLine("{ \"type\": \"invoke_agent\", \"agentName\": \"<name>\", \"task\": \"<task description>\" }");
        sb.AppendLine();
        sb.AppendLine("### sleep");
        sb.AppendLine("Pause for a duration (ISO 8601).");
        sb.AppendLine("{ \"type\": \"sleep\", \"duration\": \"PT1H\" }");
        sb.AppendLine();
        sb.AppendLine("### prompt");
        sb.AppendLine("Make an LLM call with context from completed steps.");
        sb.AppendLine("{ \"type\": \"prompt\", \"prompt\": \"<prompt text>\" }");
        sb.AppendLine();
        sb.AppendLine("### wait_for_input");
        sb.AppendLine("Pause and wait for human input.");
        sb.AppendLine("{ \"type\": \"wait_for_input\", \"description\": \"<what input is needed>\", \"inputName\": \"<optional name>\" }");
        sb.AppendLine();

        // Reference syntax
        sb.AppendLine("## Reference Syntax");
        sb.AppendLine();
        sb.AppendLine("Use {step_id} in string fields (tool arguments, agent task, prompt text) to inject");
        sb.AppendLine("the result of a completed step. The referenced step must be a direct or transitive");
        sb.AppendLine("dependency (listed in dependsOn).");
        sb.AppendLine();

        // Available tools
        if (availableTools is not null)
        {
            List<AITool> toolList = availableTools.ToList();
            if (toolList.Count > 0)
            {
                sb.AppendLine("## Available Tools");
                sb.AppendLine();
                foreach (AITool tool in toolList)
                {
                    sb.Append("- **").Append(tool.Name).Append("**");
                    if (!string.IsNullOrWhiteSpace(tool.Description))
                    {
                        sb.Append(": ").Append(tool.Description);
                    }

                    sb.AppendLine();
                }

                sb.AppendLine();
            }
        }

        // Available agents
        if (availableAgentNames is not null)
        {
            List<string> agentList = availableAgentNames.ToList();
            if (agentList.Count > 0)
            {
                sb.AppendLine("## Available Agents");
                sb.AppendLine();
                foreach (string name in agentList)
                {
                    sb.Append("- ").AppendLine(name);
                }

                sb.AppendLine();
            }
        }

        // Constraints
        sb.AppendLine("## Constraints");
        sb.AppendLine();
        sb.Append("- Maximum steps: ").Append(options.MaxSteps).AppendLine();
        sb.Append("- Maximum agent nesting depth: ").Append(options.MaxAgentNestingDepth).AppendLine();
        sb.AppendLine();

        // Best practices
        sb.AppendLine("## Best Practices");
        sb.AppendLine();
        sb.AppendLine("- Parallelize independent steps by omitting dependsOn (or using an empty list).");
        sb.AppendLine("- Use wait_for_input when a decision requires human judgment.");
        sb.AppendLine("- Use prompt steps to synthesize or reason over results from multiple steps.");
        sb.AppendLine("- Keep step IDs short and descriptive (e.g., \"fetch_data\", \"summarize\").");
        sb.AppendLine("- Reference prior step results with {step_id} instead of duplicating work.");

        // Additional instructions
        if (!string.IsNullOrWhiteSpace(additionalInstructions))
        {
            sb.AppendLine();
            sb.AppendLine("## Additional Instructions");
            sb.AppendLine();
            sb.AppendLine(additionalInstructions);
        }

        return sb.ToString();
    }
}
