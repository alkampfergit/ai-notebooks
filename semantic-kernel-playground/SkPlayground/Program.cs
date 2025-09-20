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
/// Demonstrates various AI reasoning scenarios using Semantic Kernel
/// </summary>
class Program
{
    private static Kernel? kernel;
    private static SchemaGuidedReasoner? reasoner;

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

        // Initialize the semantic kernel and reasoner
        await InitializeKernel();

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
    /// Initialize the Semantic Kernel with Azure OpenAI configuration
    /// Sets up logging, HTTP client, and creates the reasoning components
    /// </summary>
    private static async Task InitializeKernel()
    {
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

                var apiKey = Dotenv.Get("OPENAI_API_KEY");
                
                var endpoint = Dotenv.Get("AZURE_ENDPOINT");
                
                //// Configure Azure OpenAI connection
                //kernelBuilder.AddAzureOpenAIChatCompletion(
                //    deploymentName: "gpt-4o-mini",
                //    apiKey: apiKey,
                //    endpoint: endpoint
                //);

                // use standard openai 
                kernelBuilder.AddOpenAIChatCompletion(
                    modelId: "gpt-4o-mini",
                    apiKey: Dotenv.Get("OPENAI_API_KEY_NOT_AZURE")
                );

                kernel = kernelBuilder.Build();

                // Custom JsonConverter options for proper serialization
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                var databaseService = new DatabaseService();
                reasoner = new SchemaGuidedReasoner(kernel, databaseService);
            });

        AnsiConsole.MarkupLine("[green]✓[/] Schema-guided reasoner initialized successfully!");
        AnsiConsole.WriteLine();
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
                    "Database Query",
                    "Complex Invoice Task", 
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
            case "database_query":
                await RunDatabaseQueryExample();
                break;
            case "complex_invoice_task":
                await RunComplexInvoiceExample();
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
    /// Example 0: Run the original Python tasks to demonstrate Schema-Guided Reasoning
    /// This matches the TASKS array from the Python original for direct comparison
    /// </summary>
    private static async Task RunOriginalPythonTasksExample()
    {
        AnsiConsole.Write(
            new Panel("[bold red]🚀 Original Python Tasks - Schema-Guided Reasoning Demo[/]")
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
                        return await reasoner!.ReasonAndActAsync(task);
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
        AnsiConsole.Write(
            new Panel("[bold yellow]🧪 Test 1: Simple Email Task[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Yellow));

        var prompt = "Send an email to john@example.com with subject 'Welcome' and body 'Thank you for joining us!'";
        
        AnsiConsole.MarkupLine($"[dim]Prompt:[/] {prompt}");
        AnsiConsole.WriteLine();

        var result = await AnsiConsole.Status()
            .StartAsync("[yellow]Processing email task...[/]", async ctx =>
            {
                return await reasoner!.ReasonAndActAsync(prompt);
            });

        AnsiConsole.Write(
            new Panel($"[green]Result:[/] {Markup.Escape(result)}")
                .Header("Email Task Complete")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green));
    }

    /// <summary>
    /// Example 2: Demonstrate database querying capabilities
    /// Shows how the AI can interact with simulated database operations
    /// </summary>
    private static async Task RunDatabaseQueryExample()
    {
        AnsiConsole.Write(
            new Panel("[bold cyan]🧪 Test 2: Database Query[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Aqua));

        var prompt = "Query the customers database to see all available customers";
        
        AnsiConsole.MarkupLine($"[dim]Prompt:[/] {prompt}");
        AnsiConsole.WriteLine();

        var result = await AnsiConsole.Status()
            .StartAsync("[cyan]Querying database...[/]", async ctx =>
            {
                return await reasoner!.ReasonAndActAsync(prompt);
            });

        AnsiConsole.Write(
            new Panel($"[green]Result:[/] {Markup.Escape(result)}")
                .Header("Database Query Complete")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green));
    }

    /// <summary>
    /// Example 3: Demonstrate complex invoice generation
    /// Shows multi-step reasoning for financial operations with discounts
    /// </summary>
    private static async Task RunComplexInvoiceExample()
    {
        AnsiConsole.Write(
            new Panel("[bold magenta]🧪 Test 3: Complex Invoice Task[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.BlueViolet));

        var prompt = "Issue an invoice to jane@example.com for LAPTOP001 and MOUSE001 with 15% discount";
        
        AnsiConsole.MarkupLine($"[dim]Prompt:[/] {prompt}");
        AnsiConsole.WriteLine();

        var result = await AnsiConsole.Status()
            .StartAsync("[magenta]Generating invoice...[/]", async ctx =>
            {
                return await reasoner!.ReasonAndActAsync(prompt);
            });

        AnsiConsole.Write(
            new Panel($"[green]Result:[/] {Markup.Escape(result)}")
                .Header("Invoice Generation Complete")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green));
    }

    /// <summary>
    /// Example 4: Demonstrate complex multi-step customer support workflow
    /// Shows advanced reasoning with multiple conditional steps and business logic
    /// </summary>
    private static async Task RunCustomerSupportWorkflowExample()
    {
        AnsiConsole.Write(
            new Panel("[bold orange1]🧪 Test 4: Customer Support Workflow[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Orange1));

        var supportRequest = """
        A customer john@example.com contacted us saying they want to purchase a gaming laptop 
        but they're a loyal customer and should get a discount. Please:
        1. Check if they're in our customer database
        2. Send them information about our gaming laptop
        3. Issue them an invoice with appropriate discount (20% for existing customers, 10% for new ones)
        """;

        AnsiConsole.MarkupLine("[dim]Support Request:[/]");
        AnsiConsole.Write(
            new Panel(supportRequest)
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey));

        var result = await AnsiConsole.Status()
            .StartAsync("[orange1]Processing support workflow...[/]", async ctx =>
            {
                return await reasoner!.ReasonAndActAsync(supportRequest);
            });

        AnsiConsole.Write(
            new Panel($"[green]Final Result:[/] {Markup.Escape(result)}")
                .Header("Customer Support Workflow Complete")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green));

        // Show conversation log for this complex example
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Rule("[bold blue]Conversation Log[/]")
                .RuleStyle("blue"));

        // Display key benefits
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel("""
            [bold green]Key Benefits Demonstrated:[/]
            • [yellow]Structured Thinking:[/] LLM breaks down complex tasks into steps
            • [yellow]Type Safety:[/] Strongly typed schemas prevent malformed tool calls
            • [yellow]Predictable Behavior:[/] Consistent reasoning patterns
            • [yellow]Easy Debugging:[/] Clear conversation logs
            • [yellow]Extensible:[/] Easy to add new tools and reasoning patterns
            """)
                .Header("Schema-Guided Reasoning Benefits")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green));
    }
}