// Copyright (c) Microsoft. All rights reserved.

using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DurableTask;
using Microsoft.DurableTask.Client.AzureManaged;
using Microsoft.DurableTask.Worker.AzureManaged;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Get the Azure OpenAI endpoint and deployment name from environment variables.
string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

// Get DTS connection string from environment variable
string dtsConnectionString = Environment.GetEnvironmentVariable("DURABLE_TASK_SCHEDULER_CONNECTION_STRING")
    ?? "Endpoint=http://localhost:8080;TaskHub=default;Authentication=None";

// Use Azure Key Credential if provided, otherwise use Azure CLI Credential.
string? azureOpenAiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
AzureOpenAIClient client = !string.IsNullOrEmpty(azureOpenAiKey)
    ? new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(azureOpenAiKey))
    : new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());

IChatClient chatClient = client.GetChatClient(deploymentName).AsIChatClient();

// Set up the planner agent. This agent has planning tools registered so the LLM can
// generate and execute multi-step plans for complex tasks.
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

// Configure the console app to host the agents with planning support.
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
    .ConfigureServices(services =>
    {
        services.ConfigureDurableAgents(
            options =>
            {
                options.AddAIAgent(plannerAgent, timeToLive: TimeSpan.FromHours(1));
                options.AddAIAgent(researcherAgent, timeToLive: TimeSpan.FromHours(1));

                // Enable planning support with custom options.
                // This registers the 5 built-in planning tools (start_plan, list_tasks,
                // get_task_status, send_task_input, cancel_task) and the plan execution
                // orchestration.
                options.AddPlanningSupport(planOptions =>
                {
                    planOptions.MaxSteps = 20;
                    planOptions.MaxConcurrentTasks = 3;
                    planOptions.EnableReplanning = true;

                    // Restrict which agents can be invoked by plan steps
                    planOptions.AllowedAgents = new HashSet<string> { ResearcherAgentName };
                });
            },
            workerBuilder: builder => builder.UseDurableTaskScheduler(dtsConnectionString),
            clientBuilder: builder => builder.UseDurableTaskScheduler(dtsConnectionString));
    })
    .Build();

await host.StartAsync();

// Get the planner agent proxy from services
IServiceProvider services = host.Services;
AIAgent agentProxy = services.GetRequiredKeyedService<AIAgent>(PlannerAgentName);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("=== Planning Sample ===");
Console.ResetColor();
Console.WriteLine("The planner agent can decompose complex tasks into multi-step plans.");
Console.WriteLine("Try asking a complex question that requires multiple research steps.");
Console.WriteLine("Type 'exit' to quit.");
Console.WriteLine();

// Create a session for the conversation
AgentSession session = await agentProxy.CreateSessionAsync();

using CancellationTokenSource cts = new();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

while (!cts.Token.IsCancellationRequested)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("You: ");
    Console.ResetColor();

    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("Planner: ");
    Console.ResetColor();

    try
    {
        AgentResponse agentResponse = await agentProxy.RunAsync(
            message: input,
            session: session,
            cancellationToken: cts.Token);

        Console.WriteLine(agentResponse.Text);
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
        Console.WriteLine();
    }

    Console.WriteLine("(Press Enter to prompt the Planner agent again)");
    _ = Console.ReadLine();
}

await host.StopAsync();
