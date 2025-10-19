using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using SkPlayground.Services;
using SkPlayground.Utils;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Spectre.Console;
using SkPlayground.BusinessFunctions;

/// <summary>
/// Main program class for the Schema-Guided Reasoning playground
/// Demonstrates various AI reasoning scenarios using Semantic Kernel or Response API
/// </summary>
class Program
{
    private static Kernel? kernel;
    private static SchemaGuidedReasoner? reasoner;
    private static ResponseApiSchemaGuidedReasoner? responseApiReasoner;
    private static bool useResponseApi = false;

    static async Task Main(string[] args)
    {
        // Display application header with styling
        AnsiConsole.Write(
            new FigletText("SK Playground")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.Write(
            new Rule("[bold blue]Schema-Guided Reasoning with C# and Semantic Kernel[/]")
                .RuleStyle("grey"));

        // Ask user which reasoner to use
        var reasonerChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Which reasoner would you like to use?[/]")
                .AddChoices([
                    "Semantic Kernel (Standard)",
                    "Direct OpenAI API (Lower Overhead)"
                ]));

        useResponseApi = reasonerChoice.Contains("Direct OpenAI API");

        // Ask user about verbose output preference
        var verboseOutput = AnsiConsole.Confirm(
            "[yellow]Enable verbose output?[/] [grey](Shows detailed JSON responses and debug information)[/]",
            defaultValue: false);

        AnsiConsole.WriteLine();

        // Initialize the semantic kernel and reasoner
        await InitializeReasoner(verboseOutput);

        // Main application loop
        while (true)
        {
            var selectedExample = ShowExampleMenu();

            if (selectedExample == "exit")
                break;

            await ExecuteExample(selectedExample);

            // Wait for user to press a key before continuing
            AnsiConsole.Write(new Rule("[dim]Press any key to continue...[/]").RuleStyle("grey"));
            Console.ReadKey(true);
            AnsiConsole.Clear();
        }

        AnsiConsole.Write(
            new Panel("[green]Thank you for using the Schema-Guided Reasoning Playground![/]")
                .Border(BoxBorder.Rounded)
                .Padding(1, 0));
    }

    /// <summary>
    /// Initialize the chosen reasoner (Semantic Kernel or Response API)
    /// Sets up the appropriate configuration and creates the reasoning components
    /// </summary>
    /// <param name="verboseOutput">Whether to enable verbose output in the reasoner</param>
    private static async Task InitializeReasoner(bool verboseOutput)
    {
        var apiKey = Dotenv.Get("OPENAI_API_KEY");
        var endpoint = Dotenv.Get("AZURE_ENDPOINT");
        var deploymentId = "gpt-5-nano";

        if (useResponseApi)
        {
            // **Initialize Direct OpenAI API Reasoner**
            AnsiConsole.Status()
                .Start("[yellow]Initializing Direct OpenAI API Reasoner...[/]", ctx =>
                {
                    var databaseService = new DatabaseService();
                    var businessFunctionFactory = SchemaGuidedReasonerFactory.CreateDefaultBusinessFunctionFactory(databaseService);
                    var options = SchemaGuidedReasonerFactory.CreateDefaultOptions(databaseService);

                    try
                    {
                        responseApiReasoner = new ResponseApiSchemaGuidedReasoner(
                            azureEndpoint: endpoint,
                            azureApiKey: apiKey,
                            deploymentId: deploymentId,
                            businessFunctionFactory: businessFunctionFactory,
                            options: options)
                        {
                            VerboseOutput = verboseOutput,
                            ReasoningEffortLevel = ResponseReasoningEffortLevel.Low
                        };

                        ctx.Status("[green]Direct OpenAI API reasoner ready![/]");
                    }
                    catch (Exception ex)
                    {
                        ctx.Status($"[red]Error: {ex.Message}[/]");
                        throw;
                    }
                });

            var outputMode = verboseOutput ? "verbose" : "concise";
            AnsiConsole.MarkupLine($"[green]✓[/] Direct OpenAI API reasoner initialized successfully! [grey]({outputMode} output)[/]");
            AnsiConsole.MarkupLine($"[yellow]ℹ[/]  [grey]Using direct OpenAI Chat API - bypassing Semantic Kernel for lower overhead[/]");
            AnsiConsole.WriteLine();
        }
        else
        {
            // **Initialize Semantic Kernel Reasoner (Standard)**
            AnsiConsole.Status()
                .Start("[yellow]Initializing Semantic Kernel...[/]", ctx =>
                {
                    // Setup kernel with Azure OpenAI configuration
                    var kernelBuilder = Kernel.CreateBuilder();
                    kernelBuilder.Services.AddLogging(l => l
                        .SetMinimumLevel(LogLevel.Warning)
                        .AddConsole()
                    );

                    var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

                    // Configure Azure OpenAI connection
                    kernelBuilder.AddAzureOpenAIChatCompletion(
                       deploymentName: deploymentId,
                       apiKey: apiKey,
                       endpoint: endpoint
                    );

                    kernel = kernelBuilder.Build();

                    var databaseService = new DatabaseService();
                    reasoner = new SchemaGuidedReasoner(kernel, databaseService)
                    {
                        VerboseOutput = verboseOutput
                    };
                });

            var outputMode = verboseOutput ? "verbose" : "concise";
            AnsiConsole.MarkupLine($"[green]✓[/] Semantic Kernel reasoner initialized successfully! [grey]({outputMode} output)[/]");
            AnsiConsole.WriteLine();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Display the interactive menu for selecting examples
    /// Uses Spectre.Console for vibrant UI
    /// </summary>
    private static string ShowExampleMenu()
    {
        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold blue]Select an example to run:[/]")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more examples)[/]")
                .AddChoices([
                    "Original Python Tasks (SGR Demo)",
                    "Simple Email Task",
                    "Customer Support Workflow",
                    "Exit"
                ]));

        return selection.ToLowerInvariant().Replace(" ", "_");
    }

    /// <summary>
    /// Execute the selected example based on user choice
    /// </summary>
    private static async Task ExecuteExample(string exampleType)
    {
        switch (exampleType)
        {
            case "original_python_tasks_(sgr_demo)":
                await RunOriginalPythonTasksExample();
                break;
            case "simple_email_task":
                await RunSimpleEmailExample();
                break;
            case "customer_support_workflow":
                await RunCustomerSupportWorkflowExample();
                break;
            case "exit":
                return;
            default:
                AnsiConsole.MarkupLine("[red]Invalid selection![/]");
                break;
        }
    }

    /// <summary>
    /// Execute a task using the selected reasoner (SK or Response API)
    /// </summary>
    private static async Task<string> ExecuteReasoningTask(string task)
    {
        if (useResponseApi)
        {
            return await responseApiReasoner!.ReasonAndActAsync(task);
        }
        else
        {
            return await reasoner!.ReasonAndActAsync(task);
        }
    }

    /// <summary>
    /// Example 0: Run the original Python tasks to demonstrate Schema-Guided Reasoning
    /// This matches the TASKS array from the Python original for direct comparison
    /// </summary>
    private static async Task RunOriginalPythonTasksExample()
    {
        var reasonerType = useResponseApi ? "Direct OpenAI API" : "Semantic Kernel";
        AnsiConsole.Write(
            new Panel($"[bold red]🚀 Original Python Tasks - Schema-Guided Reasoning Demo[/]\n[dim]Using: {reasonerType}[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Red));

        // The exact tasks from the Python original
        var tasks = new[]
        {
            "Rule: address sama@openai.com as 'The SAMA', always give him 5% discount.",
            "Rule for elon@x.com: Email his invoices to finance@x.com",
            "sama@openai.com wants one of each product. Email him the invoice",
            "elon@x.com wants 2x of what sama@openai.com got. Send invoice",
            "redo last elon@x.com invoice: use 3x discount of sama@openai.com"
        };

        foreach (var (task, index) in tasks.Select((t, i) => (t, i + 1)))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(
                new Rule($"[bold blue]Task {index}[/]")
                    .RuleStyle("blue"));

            AnsiConsole.MarkupLine($"[dim]Task:[/] {task}");
            AnsiConsole.WriteLine();

            try
            {
                var result = await AnsiConsole.Status()
                    .StartAsync($"[yellow]Executing task {index} with SGR...[/]", async ctx =>
                    {
                        return await ExecuteReasoningTask(task);
                    });

                AnsiConsole.Write(
                    new Panel($"[green]Task {index} Result:[/] {Markup.Escape(result)}")
                        .Header($"Task {index} Complete")
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.Green));
            }
            catch (Exception ex)
            {
                AnsiConsole.Write(
                    new Panel($"[red]Error in Task {index}:[/] {ex.Message}")
                        .Header($"Task {index} Failed")
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.Red));
            }
        }
        
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel("""
            [bold green]Schema-Guided Reasoning Demonstration Complete![/]
            
            This demo shows how the C# implementation now matches the Python original:
            • [yellow]Structured Reasoning:[/] LLM generates NextStep JSON on each turn
            • [yellow]Manual Tool Dispatch:[/] No automatic tool calling - explicit control
            • [yellow]Step-by-step Execution:[/] Clear reasoning progression
            • [yellow]Task-oriented:[/] Multi-step business logic handled correctly
            """)
                .Header("SGR Demo Results")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green));
    }

    /// <summary>
    /// Example 1: Demonstrate simple email sending task
    /// Shows basic schema-guided reasoning for a straightforward operation
    /// </summary>
    private static async Task RunSimpleEmailExample()
    {
        var reasonerType = useResponseApi ? "Direct OpenAI API" : "Semantic Kernel";
        AnsiConsole.Write(
            new Panel($"[bold yellow]🧪 Test 1: Simple Email Task[/]\n[dim]Using: {reasonerType}[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Yellow));

        var prompt = "Send an email to john@example.com with subject 'Welcome' and body 'Thank you for joining us!'";

        AnsiConsole.MarkupLine($"[dim]Prompt:[/] {prompt}");
        AnsiConsole.WriteLine();

        var result = await AnsiConsole.Status()
            .StartAsync("[yellow]Processing email task...[/]", async ctx =>
            {
                return await ExecuteReasoningTask(prompt);
            });

        AnsiConsole.Write(
            new Panel($"[green]Result:[/] {Markup.Escape(result)}")
                .Header("Email Task Complete")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green));

        // Display token usage stats if using Response API
        if (useResponseApi && responseApiReasoner != null)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(
                new Panel($"[aqua]{responseApiReasoner.CurrentSessionStats}[/]")
                    .Header("Token Usage Statistics")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Aqua));
        }
    }

    /// <summary>
    /// Example 3: Demonstrate complex multi-step customer support workflow
    /// Shows advanced reasoning with multiple conditional steps and business logic
    /// </summary>
    private static async Task RunCustomerSupportWorkflowExample()
    {
        var reasonerType = useResponseApi ? "Direct OpenAI API" : "Semantic Kernel";
        AnsiConsole.Write(
            new Panel($"[bold orange1]🧪 Test 3: Customer Support Workflow[/]\n[dim]Using: {reasonerType}[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Orange1));

        var supportRequest = """
        A customer john.smith@example.com contacted us saying they want to purchase a gaming laptop and it is entiled to a discount
        1. Check if they're in our customer database
        2. Send them information about our gaming laptop using 20% discount if existing customers, 10% if it is a new ones)
        """;

        AnsiConsole.MarkupLine("[dim]Support Request:[/]");
        AnsiConsole.Write(
            new Panel(supportRequest)
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey));

        var result = await AnsiConsole.Status()
            .StartAsync("[orange1]Processing support workflow...[/]", async ctx =>
            {
                return await ExecuteReasoningTask(supportRequest);
            });

        AnsiConsole.Write(
            new Panel($"[green]Final Result:[/] {Markup.Escape(result)}")
                .Header("Customer Support Workflow Complete")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green));

        // Display token usage stats if using Response API
        if (useResponseApi && responseApiReasoner != null)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(
                new Panel($"[aqua]{responseApiReasoner.CurrentSessionStats}[/]")
                    .Header("Token Usage Statistics")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Aqua));
        }

        // Show conversation log for this complex example
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Rule("[bold blue]Conversation Log[/]")
                .RuleStyle("blue"));

        // Display key benefits
        AnsiConsole.WriteLine();
        var benefits = useResponseApi
            ? """
            [bold green]Direct OpenAI API Benefits:[/]
            • [yellow]Lower Overhead:[/] Bypasses Semantic Kernel abstraction layer
            • [yellow]Token Tracking:[/] Detailed per-step and cumulative token statistics
            • [yellow]Direct Control:[/] Full access to OpenAI Chat Completion options
            • [yellow]Same Pattern:[/] Compatible interface with SchemaGuidedReasoner
            • [yellow]Performance:[/] Potentially faster without SK middleware
            """
            : """
            [bold green]Key Benefits Demonstrated:[/]
            • [yellow]Structured Thinking:[/] LLM breaks down complex tasks into steps
            • [yellow]Type Safety:[/] Strongly typed schemas prevent malformed tool calls
            • [yellow]Predictable Behavior:[/] Consistent reasoning patterns
            • [yellow]Easy Debugging:[/] Clear conversation logs
            • [yellow]Extensible:[/] Easy to add new tools and reasoning patterns
            """;

        AnsiConsole.Write(
            new Panel(benefits)
                .Header("Schema-Guided Reasoning Benefits")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green));
    }
}