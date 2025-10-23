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
using SkPlayground.SqlScenario.SqlServer.SqlUtils;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Responses;

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
        System.Data.Common.DbProviderFactories.RegisterFactory(
            "Microsoft.Data.SqlClient",
            Microsoft.Data.SqlClient.SqlClientFactory.Instance);
        // DataAccess.SetConnectionString(
        //     "Server=localhost\\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;",
        //     "Microsoft.Data.SqlClient",
        //    NullLogger.Instance);
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
        var deploymentId = "gpt-5-mini";

        // **Always create a Semantic Kernel instance**
        // This is required even for Response API mode because BusinessFunctionFactory needs it
        // for functions like ExecuteSqlQueryFunction that use the kernel for natural language translation
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
                ctx.Status("[green]Semantic Kernel initialized![/]");
            });

        if (useResponseApi)
        {
            // **Initialize Direct OpenAI API Reasoner**
            // Note: Still uses kernel for business functions that need LLM capabilities
            AnsiConsole.Status()
                .Start("[yellow]Initializing Direct OpenAI API Reasoner...[/]", ctx =>
                {
                    var databaseService = new DatabaseService();
                    // Pass the kernel to the factory so business functions can use LLM capabilities
                    var businessFunctionFactory = SchemaGuidedReasonerFactory.CreateDefaultBusinessFunctionFactory(
                        databaseService,
                        kernel);
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
#pragma warning disable OPENAI001
                            ReasoningEffortLevel = ResponseReasoningEffortLevel.Low
#pragma warning restore OPENAI001
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
            AnsiConsole.MarkupLine($"[yellow]ℹ[/]  [grey]Using direct OpenAI Chat API for reasoning - Semantic Kernel available for business functions[/]");
            AnsiConsole.WriteLine();
        }
        else
        {
            // **Initialize Semantic Kernel Reasoner (Standard)**
            AnsiConsole.Status()
                .Start("[yellow]Initializing Semantic Kernel Reasoner...[/]", ctx =>
                {
                    var databaseService = new DatabaseService();
                    reasoner = new SchemaGuidedReasoner(kernel!, databaseService)
                    {
                        VerboseOutput = verboseOutput
                    };
                    ctx.Status("[green]Semantic Kernel reasoner ready![/]");
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
                    "Search table in database",
                    "SQL Database + Excel Export",
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
            case "sql_database_+_excel_export":
                await RunSqlExample("Please give me a summary of all the orders grouped by year and customer code in the database northwind exported in excel");
                break;
            case "search_table_in_database":
                await RunSqlExample("Is there a table order in the northwind database?");
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
        // **Initialize a fresh StateManager instance for this scenario execution**
        StateManager.Start();

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
        // **Initialize a fresh StateManager instance for this scenario execution**
        StateManager.Start();

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
        // **Initialize a fresh StateManager instance for this scenario execution**
        StateManager.Start();

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

    /// <summary>
    /// Example 4: Demonstrate SQL database query with Excel export
    /// Creates a specialized reasoner with only SQL and Excel functions
    /// Shows how to use schema-guided reasoning for data extraction and export workflows
    /// </summary>
    private static async Task RunSqlExample(string userRequest)
    {
        // **Initialize a fresh StateManager instance for this scenario execution**
        StateManager.Start();

        var reasonerType = useResponseApi ? "Direct OpenAI API" : "Semantic Kernel";
        AnsiConsole.Write(
            new Panel($"[bold aqua]🧪 Test 4: SQL Database + Excel Export[/]\n[dim]Using: {reasonerType}[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Aqua));

        AnsiConsole.MarkupLine("[yellow]Creating specialized reasoner with SQL and Excel functions only...[/]");
        AnsiConsole.WriteLine();

        // **Create a specialized reasoner that contains only SQL and Excel export functions**
        // This demonstrates how to compose custom reasoners for specific workflows
        var apiKey = Dotenv.Get("OPENAI_API_KEY");
        var endpoint = Dotenv.Get("AZURE_ENDPOINT");
        var deploymentId = "gpt-5-nano";
        var databaseService = new DatabaseService();
        var sqlServerService = new SqlServerService();

        // **Define the specific tool types for SQL workflow**
        var sqlToolTypes = new Type[]
        {
            typeof(ReportTaskCompletionToolCall),
            typeof(GetDatabaseNamesFromServerToolCall),
            typeof(GetSqlDatabaseSchemaToolCall),
            typeof(ExecuteSqlQueryToolCall),
            typeof(ExportSqlQueryResultToolCall)
        };

        // **Create a specialized BusinessFunctionFactory with only SQL-related functions**
        var loggerFactory = kernel.Services.GetRequiredService<ILoggerFactory>();
        var sqlFunctionFactory = new BusinessFunctionFactory(databaseService, sqlServerService, kernel, loggerFactory, sqlToolTypes);

        // **Create custom options for SQL workflow**
        var sqlOptions = new SchemaGuidedReasonerOptions
        {
            JsonSerializerOptions = SchemaGuidedReasonerOptions.CreateDefaultJsonOptions(),
            SystemPromptBuilder = (context, _) =>
            {
                var toolsSummary = SchemaGuidedReasoner.GenerateToolsSummary(context.Schema.AvailableTools);
                return $@"You are a SQL data analyst assistant.

IMPORTANT: You must always respond with structured JSON that includes:
1. Current state analysis
2. List of remaining steps to execute to complete the task
3. Whether the task is completed
4. Function to call, it contains parameter to execute the first remaining step

## IMPORTANT
- Please check carefully the status of the system to create the most relevant next function to call
- You should examine carefully what function were already called and their results
- Answer to the user only when you have the response to user question or you cannot proceed further 

## Available Tools:
{toolsSummary}

## General rules
- If you want to check available databases use GetDatabaseNamesFromServer
- If you need to know table names and columns use GetSqlDatabaseSchema
- You can use the ExecuteSqlQuery function to execute a query expressed in natural language.
- If you need to export a query you need to execute first with ExecuteSqlQuery giving a resultId then call the exportSqlQueryResult function with the resultId obtained.
- When there are not anymore steps to execute, you can use the ReportTaskCompletion function to report the final result to the user. Tell if the process was successful or not.

";
            }
        };

        AnsiConsole.MarkupLine("[dim]User Request:[/]");
        AnsiConsole.Write(
            new Panel(userRequest)
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey));

        string result;
        if (useResponseApi)
        {
            // **Create specialized Response API reasoner**
            var sqlReasoner = new ResponseApiSchemaGuidedReasoner(
                azureEndpoint: endpoint,
                azureApiKey: apiKey,
                deploymentId: deploymentId,
                businessFunctionFactory: sqlFunctionFactory,
                options: sqlOptions)
            {
                VerboseOutput = responseApiReasoner!.VerboseOutput,
#pragma warning disable OPENAI001
                ReasoningEffortLevel = ResponseReasoningEffortLevel.Low 
#pragma warning restore OPENAI001
            };

            result = await AnsiConsole.Status()
                .StartAsync("[aqua]Processing SQL + Excel workflow...[/]", async ctx =>
                {
                    return await sqlReasoner.ReasonAndActAsync(userRequest);
                });

            // Display token usage stats
            AnsiConsole.WriteLine();
            AnsiConsole.Write(
                new Panel($"[aqua]{sqlReasoner.CurrentSessionStats}[/]")
                    .Header("Token Usage Statistics")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Aqua));
        }
        else
        {
            // **Create specialized Semantic Kernel reasoner**
            var sqlReasoner = new SchemaGuidedReasoner(
                kernel!,
                sqlFunctionFactory,
                sqlOptions)
            {
                VerboseOutput = reasoner!.VerboseOutput
            };

            result = await AnsiConsole.Status()
                .StartAsync("[aqua]Processing SQL + Excel workflow...[/]", async ctx =>
                {
                    return await sqlReasoner.ReasonAndActAsync(userRequest);
                });
        }

        AnsiConsole.Write(
            new Panel($"[green]Final Result:[/] {Markup.Escape(result)}")
                .Header("SQL + Excel Workflow Complete")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green));

        // Show benefits of specialized reasoners
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Panel("""
            [bold green]Specialized Reasoner Benefits:[/]
            • [yellow]Focused Context:[/] Only SQL and Excel tools available, reducing complexity
            • [yellow]Custom Prompts:[/] Tailored system prompt for SQL workflow
            • [yellow]Type Safety:[/] Strongly typed SQL and Excel function calls
            • [yellow]Composability:[/] Easy to create domain-specific reasoners
            • [yellow]Multi-step Workflow:[/] Automatic orchestration of database discovery, querying, and export
            """)
                .Header("Specialized SQL Reasoner")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green));
    }
}