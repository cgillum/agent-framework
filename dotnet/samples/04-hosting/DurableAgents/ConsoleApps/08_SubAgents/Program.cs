// Copyright (c) Microsoft. All rights reserved.

using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DurableTask;
using Microsoft.Agents.AI.DurableTask.BuiltInTools;
using Microsoft.DurableTask.Client.AzureManaged;
using Microsoft.DurableTask.Worker.AzureManaged;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

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

// Create the built-in sub-agent tool with optional lifecycle hooks
RunSubAgentTool subAgentTool = new(new RunSubAgentToolOptions
{
    MaxNestingDepth = 2,
    Timeout = TimeSpan.FromMinutes(5),
    OnBeforeSubAgentRun = async (context, ct) =>
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"  [Hook] Creating sub-agent '{context.AgentName}' (session: {context.SubAgentSessionId.Key[..8]}...)");
        Console.ResetColor();
        await Task.CompletedTask;
    },
    OnAfterSubAgentRun = async (context, ct) =>
    {
        string status = context.TimedOut ? "timed out" : context.Exception is not null ? "failed" : "completed";
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"  [Hook] Sub-agent '{context.AgentName}' {status}");
        Console.ResetColor();
        await Task.CompletedTask;
    },
});

// Set up two agents: a coordinator and a researcher.
// The coordinator can delegate research tasks to the researcher sub-agent.
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

// Configure the console app to host both agents.
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
    .ConfigureServices(services =>
    {
        services.ConfigureDurableAgents(
            options =>
            {
                options.AddAIAgent(coordinatorAgent, timeToLive: TimeSpan.FromHours(1));
                options.AddAIAgent(researcherAgent, timeToLive: TimeSpan.FromHours(1));
            },
            workerBuilder: builder => builder.UseDurableTaskScheduler(dtsConnectionString),
            clientBuilder: builder => builder.UseDurableTaskScheduler(dtsConnectionString));
    })
    .Build();

await host.StartAsync();

// Get the coordinator agent proxy from services
IServiceProvider services = host.Services;
AIAgent agentProxy = services.GetRequiredKeyedService<AIAgent>(CoordinatorName);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("=== Sub-Agent Console Sample ===");
Console.ResetColor();
Console.WriteLine("The coordinator agent can delegate tasks to a 'researcher' sub-agent.");
Console.WriteLine("Try asking complex questions that require in-depth analysis.");
Console.WriteLine("Type 'exit' to quit.");
Console.WriteLine();

// Create a session for the conversation
AgentSession session = await agentProxy.CreateSessionAsync();

while (true)
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
    Console.Write("Coordinator: ");
    Console.ResetColor();

    try
    {
        AgentResponse agentResponse = await agentProxy.RunAsync(
            message: input,
            session: session,
            cancellationToken: CancellationToken.None);

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
}

await host.StopAsync();
