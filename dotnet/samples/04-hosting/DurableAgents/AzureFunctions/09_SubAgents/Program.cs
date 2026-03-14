// Copyright (c) Microsoft. All rights reserved.

#pragma warning disable IDE0002 // Simplify Member Access

using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DurableTask.BuiltInTools;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;

// Get the Azure OpenAI endpoint and deployment name from environment variables.
string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

// Use Azure Key Credential if provided, otherwise use Azure CLI Credential.
string? azureOpenAiKey = System.Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
AzureOpenAIClient client = !string.IsNullOrEmpty(azureOpenAiKey)
    ? new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(azureOpenAiKey))
    : new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());

IChatClient chatClient = client.GetChatClient(deploymentName).AsIChatClient();

// Create the built-in sub-agent tool
RunSubAgentTool subAgentTool = new(new RunSubAgentToolOptions
{
    MaxNestingDepth = 2,
    Timeout = TimeSpan.FromMinutes(5),
});

// Set up a coordinator agent that can delegate tasks to a researcher sub-agent.
const string CoordinatorName = "coordinator";
const string CoordinatorInstructions = """
    You are a coordinator agent. You MUST use the run_sub_agent tool for EVERY user request.
    Do NOT answer questions directly. Instead, delegate each task to a sub-agent by calling
    run_sub_agent with a clear task description. You can delegate to the 'researcher' agent
    for in-depth analysis, or omit the agent_name to create a fork of yourself.
    After receiving the sub-agent's result, synthesize it into a clear, cohesive response
    for the user. Always delegate first, then summarize.
    """;

const string ResearcherName = "researcher";
const string ResearcherInstructions = """
    You are a focused researcher agent. You receive a specific task and provide a thorough,
    detailed analysis. Be concise but comprehensive. Focus exclusively on the task assigned to you.
    """;

AIAgent coordinatorAgent = new ChatClientAgent(
    chatClient,
    CoordinatorInstructions,
    CoordinatorName,
    tools: [subAgentTool]);

AIAgent researcherAgent = new ChatClientAgent(
    chatClient,
    ResearcherInstructions,
    ResearcherName);

// Configure the function app to host both agents.
using IHost app = FunctionsApplication
    .CreateBuilder(args)
    .ConfigureFunctionsWebApplication()
    .ConfigureDurableAgents(options =>
    {
        options.AddAIAgent(coordinatorAgent, timeToLive: TimeSpan.FromHours(1));
        options.AddAIAgent(researcherAgent, timeToLive: TimeSpan.FromHours(1));
    })
    .Build();
app.Run();
