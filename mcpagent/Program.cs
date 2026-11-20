using System;
using System.Collections.Generic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

Console.WriteLine("Application starting");

string githubToken = "<github-token>";
string localRepoPath = @"I:\ConsoleApp";

var githubTransportOptions = new StdioClientTransportOptions
{
    Name = "GitHubServer",
    Command = "npx",
    Arguments = ["-y", "@modelcontextprotocol/server-github"],
    EnvironmentVariables = new Dictionary<string, string>
    {
        { "GITHUB_PERSONAL_ACCESS_TOKEN", githubToken }
    }
};

var githubTransport = new StdioClientTransport(githubTransportOptions);
await using var githubClient = await McpClient.CreateAsync(githubTransport);

var fsTransportOptions = new StdioClientTransportOptions
{
    Name = "FileSystemServer",
    Command = "npx",
    Arguments = ["-y", "@modelcontextprotocol/server-filesystem", localRepoPath]
};

var fsTransport = new StdioClientTransport(fsTransportOptions);
await using var fsClient = await McpClient.CreateAsync(fsTransport);

// Configure the tools for the AI model 
var allTools = new List<AITool>();

var githubToolsResult = await githubClient.ListToolsAsync();
allTools.AddRange(githubToolsResult);

var fsToolsResult = await fsClient.ListToolsAsync();
allTools.AddRange(fsToolsResult);

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Information);
});

IChatClient ollamaClient = new OllamaChatClient(new Uri("http://localhost:11434"), "qwen2.5:7b");

IChatClient chatClient = new ChatClientBuilder(ollamaClient)
    .UseFunctionInvocation(loggerFactory: loggerFactory) // லாகர் இணைக்கப்பட்டுள்ளது
    .Build();

//Prompt for the AI agent to perform tasks in sequence
string developerPrompt = $"""
    You are an expert .NET developer agent with access to the local file system and GitHub.
    
    IMPORTANT CONTEXT FOR GITHUB TOOLS:
    - Repository Owner: "rajasudhans"
    - Repository Name: "ConsoleApp"
    Please perform the following tasks in sequence:
    1. Scan the C# project in the local directory (`{localRepoPath}`).
    2. Modify the code to print the message "Hello. New to AI.".
    3. push the changes in branch "ai-updates" in the GitHub repository "rajasudhans/ConsoleApp".
    4. Commit and push these code changes to that branch.
    5. Raise a Pull Request (PR) for these changes in the master branch of the repository.
    """;

Console.WriteLine("\nAI Agent process started...");

// Tools are provided in the chat options to the AI model, so it can use them to perform the tasks.
var chatOptions = new ChatOptions
{
    Tools = allTools
};

// Model is prompted to perform the tasks in sequence, and the response is captured.
var response = await chatClient.GetResponseAsync(developerPrompt, chatOptions);

Console.WriteLine("\n🎯 AI Agent report:");
foreach (var message in response.Messages)
{
    Console.WriteLine($"[{message.Role}]: {message.Text}");
}