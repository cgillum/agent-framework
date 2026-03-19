// Copyright (c) Microsoft. All rights reserved.

#pragma warning disable IDE0002 // Simplify Member Access

using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
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
string? azureOpenAiKey = System.Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
AzureOpenAIClient client = !string.IsNullOrEmpty(azureOpenAiKey)
    ? new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(azureOpenAiKey))
    : new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());

IChatClient chatClient = client.GetChatClient(deploymentName).AsIChatClient();

// Set up the planner agent with instructions for when to use planning.
const string PlannerAgentName = "planner";
const string PlannerAgentInstructions = """
    You are a planning agent that decomposes complex tasks into structured multi-step plans.
    When given a complex request, use the start_plan tool to create and execute a plan.
    Use list_tasks and get_task_status to monitor progress.
    Use send_task_input when a plan step is waiting for human input.
    Use cancel_task to abort a running plan if needed.
    For simple questions, answer directly without creating a plan.
    """;

// Set up a researcher agent that can be invoked by plan steps via invoke_agent.
const string ResearcherAgentName = "researcher";
const string ResearcherAgentInstructions = """
    You are a focused researcher agent. You receive a specific research task and provide
    a thorough, detailed analysis. Be concise but comprehensive.
    """;

AIAgent plannerAgent = new ChatClientAgent(
    chatClient,
    PlannerAgentInstructions,
    PlannerAgentName);

AIAgent researcherAgent = new ChatClientAgent(
    chatClient,
    ResearcherAgentInstructions,
    ResearcherAgentName);

// Configure the function app to host agents with planning support.
using IHost app = FunctionsApplication
    .CreateBuilder(args)
    .ConfigureFunctionsWebApplication()
    .ConfigureDurableAgents(options =>
    {
        options.AddAIAgent(plannerAgent, timeToLive: TimeSpan.FromHours(1));
        options.AddAIAgent(researcherAgent, timeToLive: TimeSpan.FromHours(1));

        // Enable planning support. This registers the 5 built-in planning tools
        // and the plan execution orchestration.
        options.AddPlanningSupport(planOptions =>
        {
            planOptions.MaxSteps = 20;
            planOptions.MaxConcurrentTasks = 3;
            planOptions.EnableReplanning = true;
            planOptions.AllowedAgents = new HashSet<string> { ResearcherAgentName };
        });
    })
    .Build();

app.Run();
