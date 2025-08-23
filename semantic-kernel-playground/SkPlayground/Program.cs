using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using SkPlayground.Models;
using SkPlayground.Services;
using SkPlayground.Utils;
using System.Text.Json.Serialization;

Console.WriteLine("=== Schema-Guided Reasoning with C# and Semantic Kernel ===");
Console.WriteLine();

// Setup kernel with OpenAI (you can change to local LM Studio)
//var redirectUrl = "https://api.openai.com/v1"; // OpenAI
// var redirectUrl = "http://10.0.0.39:1234/v1"; // Local LM Studio
var redirectUrl = Dotenv.Get("AZURE_ENDPOINT");

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Services.AddLogging(l => l
    .SetMinimumLevel(LogLevel.Warning)
    .AddConsole()
);

var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

// kernelBuilder.AddOpenAIChatCompletion(
//     modelId: "gpt4omini", 
//     apiKey: Dotenv.Get("OPENAI_API_KEY"),
//     endpoint: new Uri(redirectUrl),
//     httpClient: httpClient);

var apiKey = Dotenv.Get("OPENAI_API_KEY");
var endpoint = Dotenv.Get("AZURE_ENDPOINT");
//we use azureopenai for this sample
kernelBuilder.AddAzureOpenAIChatCompletion(
    deploymentName: "gpt4omini",
    apiKey: apiKey,
    endpoint: endpoint
);

var kernel = kernelBuilder.Build();

//now quickly test if the kernel is working
var result = await kernel.InvokePromptAsync("Who are you?");
Console.WriteLine(result);

// Custom JsonConverter options for proper serialization
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    Converters = { new JsonStringEnumConverter() }
};



var dispatcher = new ToolDispatcher(jsonOptions);
var reasoner = new SchemaGuidedReasoner(kernel, dispatcher, jsonOptions);

Console.WriteLine("Schema-guided reasoner initialized!");
Console.WriteLine();

// Test 1: Simple email task
Console.WriteLine("🧪 Test 1: Simple Email Task");
Console.WriteLine("================================");

var result1 = await reasoner.ReasonAndActAsync("Send an email to john@example.com with subject 'Welcome' and body 'Thank you for joining us!'");
Console.WriteLine($"Result: {result1}");
Console.WriteLine();

// Test 2: Database query
Console.WriteLine("🧪 Test 2: Database Query");
Console.WriteLine("==========================");

var result2 = await reasoner.ReasonAndActAsync("Query the customers database to see all available customers");
Console.WriteLine($"Result: {result2}");
Console.WriteLine();

// Test 3: Complex invoice task
Console.WriteLine("🧪 Test 3: Complex Invoice Task");
Console.WriteLine("================================");

var result3 = await reasoner.ReasonAndActAsync("Issue an invoice to jane@example.com for LAPTOP001 and MOUSE001 with 15% discount");
Console.WriteLine($"Result: {result3}");
Console.WriteLine();

// Test 4: Customer support workflow
Console.WriteLine("🧪 Test 4: Customer Support Workflow");
Console.WriteLine("=====================================");

var supportRequest = """
A customer john@example.com contacted us saying they want to purchase a gaming laptop 
but they're a loyal customer and should get a discount. Please:
1. Check if they're in our customer database
2. Send them information about our gaming laptop
3. Issue them an invoice with appropriate discount (20% for existing customers, 10% for new ones)
""";

Console.WriteLine($"Support Request: {supportRequest}");
Console.WriteLine();

var step1 = await reasoner.ReasonAndActAsync(supportRequest);
Console.WriteLine($"Step 1 Result: {step1}\n");

var step2 = await reasoner.ReasonAndActAsync("Continue with the next step in the workflow");
Console.WriteLine($"Step 2 Result: {step2}\n");

var step3 = await reasoner.ReasonAndActAsync("Continue with the final step");
Console.WriteLine($"Step 3 Result: {step3}\n");

// Print final conversation log
reasoner.PrintConversationLog();

Console.WriteLine("=== Schema-Guided Reasoning Demo Complete ===");
Console.WriteLine();
Console.WriteLine("Key Benefits Demonstrated:");
Console.WriteLine("- Structured Thinking: LLM breaks down complex tasks into steps");
Console.WriteLine("- Type Safety: Strongly typed schemas prevent malformed tool calls");
Console.WriteLine("- Predictable Behavior: Consistent reasoning patterns");
Console.WriteLine("- Easy Debugging: Clear conversation logs");
Console.WriteLine("- Extensible: Easy to add new tools and reasoning patterns");