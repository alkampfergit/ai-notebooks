using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Newtonsoft.Json;
using Alkampfer.Sgr.BusinessFunctions;
using Alkampfer.Sgr.Models;
using Alkampfer.Sgr.Utils;
using Alkampfer.Sgr.Services;
using Alkampfer.Sgr.Playground.BusinessFunctions;
using Spectre.Console;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Alkampfer.Sgr.Playground.Services;

/// <summary>
/// Represents the result of a reasoning step containing both the NextStep object and function name.
/// </summary>
/// <param name="NextStep">The deserialized NextStep object containing the reasoning parameters and tool call</param>
/// <param name="FunctionName">The name of the function that the LLM chose to call (derived from ToolCall discriminator)</param>
public readonly record struct NextStepResult(NextStep NextStep, string FunctionName);

/// <summary>
/// Represents the complete LLM response including both parsed results and the original assistant message.
/// </summary>
/// <param name="NextStepResults">Array of parsed NextStep results</param>
/// <param name="AssistantResponse">The original assistant message containing structured JSON response</param>
public readonly record struct LLMReasoningResponse(NextStepResult[] NextStepResults, ChatMessageContent AssistantResponse);

/// <summary>
/// Implements Schema-Guided Reasoning (SGR) pattern where the LLM is forced to generate
/// a JSON object conforming to the NextStep schema on every turn, enabling deliberate
/// step-by-step reasoning with manual tool dispatch.
/// </summary>
public class SchemaGuidedReasoner
{
    private readonly IChatCompletionService _chatService;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly BusinessFunctionFactory _functionFactory;
    private readonly SchemaGuidedReasonerOptions _options;

    /// <summary>
    /// Controls whether to display detailed debug output during reasoning.
    /// When false, only essential information is shown.
    /// </summary>
    public bool VerboseOutput { get; set; } = true;

    private record ToolExecutionResult(string ToolName, string Summary);

    /// <summary>
    /// Initializes the reasoner with a chat service, business function factory, and optional configuration.
    /// </summary>
    public SchemaGuidedReasoner(
        IChatCompletionService chatService,
        BusinessFunctionFactory businessFunctionFactory,
        SchemaGuidedReasonerOptions? options = null)
    {
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        _functionFactory = businessFunctionFactory ?? throw new ArgumentNullException(nameof(businessFunctionFactory));

        _options = options ?? SchemaGuidedReasonerOptions.CreateDefault();
        _jsonOptions = _options.JsonSerializerOptions ?? SchemaGuidedReasonerOptions.CreateDefaultJsonOptions();
    }

    /// <summary>
    /// Convenience constructor that accepts a kernel and extracts the chat completion service.
    /// </summary>
    public SchemaGuidedReasoner(
        Kernel kernel,
        BusinessFunctionFactory businessFunctionFactory,
        SchemaGuidedReasonerOptions? options = null)
        : this(
            kernel?.GetRequiredService<IChatCompletionService>() ?? throw new ArgumentNullException(nameof(kernel)),
            businessFunctionFactory,
            options)
    {
    }

    /// <summary>
    /// Legacy constructor retained for backwards compatibility. Prefer SchemaGuidedReasonerFactory.
    /// </summary>
    [Obsolete("Use SchemaGuidedReasonerFactory.CreateDefault to instantiate.")]
    public SchemaGuidedReasoner(
        Kernel kernel,
        DatabaseService databaseService)
        : this(
            kernel,
            SchemaGuidedReasonerFactory.CreateDefaultBusinessFunctionFactory(databaseService),
            SchemaGuidedReasonerFactory.CreateDefaultOptions(databaseService))
    {
    }

    /// <summary>
    /// Generates dynamic system prompt based on available tools using the configured prompt builder.
    /// </summary>
    private string GenerateSystemPrompt(IEnumerable<Type>? availableToolTypes = null)
    {
        var schemaResult = availableToolTypes != null
            ? _functionFactory.GenerateSchemaWithDocumentationForToolCall(availableToolTypes)
            : _functionFactory.GenerateSchemaWithDocumentationForToolCall();

        var context = new SchemaGuidedReasonerPromptContext(
            schemaResult,
            _functionFactory,
            _options.CustomContext);

        return _options.SystemPromptBuilder(context, availableToolTypes);
    }

    /// <summary>
    /// Determines which tools should be available using the optional selector in options.
    /// </summary>
    private IEnumerable<Type>? DetermineAvailableTools(string userRequest, List<ToolExecutionResult> executedTasks)
    {
        if (_options.ToolTypeSelector is null)
        {
            return null;
        }

        var history = executedTasks
            .Select(t => new SchemaGuidedReasonerToolHistory(t.ToolName, t.Summary))
            .ToList()
            .AsReadOnly();

        return _options.ToolTypeSelector(userRequest, history);
    }

    /// <summary>
    /// Generates a summary of available tools from tool information array.
    /// </summary>
    internal static string GenerateToolsSummary(ToolInformation[] availableTools)
    {
        var summary = new StringBuilder();

        foreach (var tool in availableTools)
        {
            summary.AppendLine($"- **{tool.ToolName}**: {tool.ToolDescription}");
        }

        return summary.ToString();
    }

    /// <summary>
    /// Execute Schema-Guided Reasoning for the given user request.
    /// This implements the core SGR pattern: force LLM to generate NextStep schema,
    /// manually dispatch tools, and continue reasoning until task completion.
    /// </summary>
    public async Task<string> ReasonAndActAsync(string userRequest)
    {
        var executionTaskResult = new List<ToolExecutionResult>();

        // Limit reasoning steps to prevent infinite loops (matching Python original)
        for (int step = 1; step <= 20; step++)
        {
            if (VerboseOutput)
            {
                AnsiConsole.Write($"[yellow]Planning step_{step}...[/] ");
            }

            try
            {
                if (VerboseOutput)
                {
                    // Make the LLM call explicit for this cycle
                    AnsiConsole.MarkupLine($"[grey](LLM call #{step})[/]");
                }

                // Force LLM to generate structured JSON response conforming to NextStep schema
                var (nextStep, assistantRaw) = await GetNextStepFromLLM(userRequest, executionTaskResult);

                if (VerboseOutput)
                {
                    // Show the raw assistant response (JSON) to aid debugging and transparency
                    AnsiConsole.MarkupLine("[grey]Assistant raw response:[/]");
                    AnsiConsole.WriteLine(Markup.Escape(string.IsNullOrWhiteSpace(assistantRaw)
                        ? "(empty response)"
                        : assistantRaw));
                }

                // Always display the planned steps list - this is valuable information even in concise mode
                if (nextStep.PlanRemainingStepsBrief != null && nextStep.PlanRemainingStepsBrief.Count > 0)
                {
                    AnsiConsole.MarkupLine("[cyan]  Planned remaining steps:[/]");
                    for (int i = 0; i < nextStep.PlanRemainingStepsBrief.Count; i++)
                    {
                        var stepText = nextStep.PlanRemainingStepsBrief[i] ?? string.Empty;
                        AnsiConsole.MarkupLine($"[cyan]    {i + 1}. {Markup.Escape(stepText)}[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[cyan]  Planned remaining steps:[/] [dim]None[/]");
                }

                // Display the NextStep information - always show this
                var currentPlan = nextStep.PlanRemainingStepsBrief?.FirstOrDefault() ?? "No plan specified";
                var toolName = nextStep.Function?.GetType().Name.Replace("ToolCall", "") ?? "unknown";

                // **Show the NextStep tool call details - always visible even in non-verbose mode**
                AnsiConsole.MarkupLine($"[cyan]  Next Step:[/] [yellow]{Markup.Escape(toolName)}[/] - {Markup.Escape(currentPlan)}");

                // Show NextStep serialized output - always visible
                try
                {
                    var nextStepJson = JsonConvert.SerializeObject(nextStep.Function, Formatting.Indented);
                    AnsiConsole.MarkupLine("[dim]  Tool parameters:[/]");
                    AnsiConsole.WriteLine(Markup.Escape(nextStepJson));
                }
                catch (Exception)
                {
                    // Fallback to type name if serialization fails
                    AnsiConsole.MarkupLine($"[dim]    (Unable to serialize NextStep for {Markup.Escape(toolName)})[/]");
                }

                if (nextStep.Function is ReportTaskCompletionToolCall completionParameter)
                {
                    if (VerboseOutput)
                    {
                        AnsiConsole.MarkupLine($"[blue]Task completed: {Markup.Escape(completionParameter.Summary)}[/]");
                    }
                    return completionParameter.Summary;
                }

                // Ensure a tool was provided and dispatch it
                if (nextStep.Function == null)
                {
                    throw new InvalidOperationException("LLM did not provide a NextStepToolToCall in the NextStep response.");
                }

                // Manually dispatch the tool function
                var businessResult = await _functionFactory.DispatchToolFunction(nextStep.Function);

                // Add execution result to the list for next iteration
                var resultSummary = businessResult.Summary;
                executionTaskResult.Add(new ToolExecutionResult(toolName, resultSummary));

                // Show result
                if (VerboseOutput)
                {
                    AnsiConsole.Write("[green]    ✓ [/]");
                    AnsiConsole.WriteLine(Markup.Escape(resultSummary));
                }
                else
                {
                    AnsiConsole.MarkupLine($"  [grey]→[/] {Markup.Escape(resultSummary)}");
                }
            }
            catch (Exception ex)
            {
                // Use WriteLine to avoid markup parsing issues with exception messages
                AnsiConsole.Write("[red]Error in reasoning step ");
                AnsiConsole.Write(step.ToString());
                AnsiConsole.Write(": [/]");
                AnsiConsole.WriteLine(ex.Message);
                return $"Error occurred during reasoning: {ex.Message}";
            }
        }

        return "Task completed after maximum reasoning steps";
    }

    /// <summary>
    /// Get structured NextStep response from LLM using JSON schema constraint.
    /// This is the core of SGR - forcing the model to generate valid NextStep JSON.
    /// Uses a single user message containing the original question and executed tasks.
    /// </summary>
    private async Task<(NextStep NextStep, string AssistantRaw)> GetNextStepFromLLM(string userRequest, List<ToolExecutionResult> executedTasks)
    {
        // Determine which tools should be available for this request
        var availableToolTypes = DetermineAvailableTools(userRequest, executedTasks);

        // Generate dynamic system prompt based on available tools
        var systemPrompt = GenerateSystemPrompt(availableToolTypes);

        // Build the user message with original question and executed tasks
        var userMessage = $"User Request: {userRequest}";

        if (executedTasks.Count > 0)
        {
            userMessage += "\n\nExecuted Tasks:";
            foreach (var task in executedTasks)
            {
                userMessage += $"\n- {task.ToolName}: {task.Summary}";
            }
        }

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(userMessage);

        // Configure OpenAI execution settings with JSON schema constraint
        // Generate schema for the same set of available tools
        var schemaStr = availableToolTypes != null
            ? _functionFactory.GenerateJsonSchemaForToolCall(availableToolTypes)
            : _functionFactory.GenerateJsonSchemaForToolCall();
        var chatResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "next_step_schema",
            jsonSchema: BinaryData.FromString(schemaStr),
            jsonSchemaIsStrict: true
        );
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = chatResponseFormat
        };

        var response = await _chatService.GetChatMessageContentAsync(chatHistory, executionSettings);

        // Parse the JSON response to NextStep object
        var openAIResponse = (OpenAIChatMessageContent)response;
        var jsonContent = openAIResponse.Content ?? string.Empty;

        var nextStep = _functionFactory.DeserializeNextStep(jsonContent);
        if (nextStep == null)
        {
            throw new InvalidOperationException("Failed to deserialize NextStep from LLM response:\n" + jsonContent);
        }

        return (nextStep, jsonContent);
    }
}

/// <summary>
/// Configuration options for SchemaGuidedReasoner.
/// </summary>
public sealed class SchemaGuidedReasonerOptions
{
    /// <summary>
    /// Delegate that builds the system prompt given the current schema documentation and optional tool subset.
    /// </summary>
    public Func<SchemaGuidedReasonerPromptContext, IEnumerable<Type>?, string> SystemPromptBuilder { get; init; } = (context, _) =>
    {
        var toolsSummary = SchemaGuidedReasoner.GenerateToolsSummary(context.Schema.AvailableTools);
        return $@"You are an AI assistant. Always respond with structured JSON that matches the provided schema.

## Available Tools:
{toolsSummary}";
    };

    /// <summary>
    /// Optional delegate that can restrict available tools based on the user request and execution history.
    /// </summary>
    public Func<string, IReadOnlyList<SchemaGuidedReasonerToolHistory>, IEnumerable<Type>?>? ToolTypeSelector { get; init; }

    /// <summary>
    /// JSON options used for serializing/deserializing tool payloads.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; init; }

    /// <summary>
    /// Arbitrary custom context passed to the SystemPromptBuilder.
    /// </summary>
    public object? CustomContext { get; init; }

    /// <summary>
    /// Creates an options instance with minimal defaults.
    /// </summary>
    public static SchemaGuidedReasonerOptions CreateDefault() => new()
    {
        JsonSerializerOptions = CreateDefaultJsonOptions()
    };

    internal static JsonSerializerOptions CreateDefaultJsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault
            ? new DefaultJsonTypeInfoResolver()
            : JsonTypeInfoResolver.Combine()
    };
}

/// <summary>
/// Prompt builder context containing schema documentation, factory metadata, and custom scenario context.
/// </summary>
/// <param name="Schema">Schema documentation for currently available tools.</param>
/// <param name="FunctionFactory">Factory responsible for dispatching tool calls.</param>
/// <param name="CustomContext">Additional scenario-specific context.</param>
public readonly record struct SchemaGuidedReasonerPromptContext(
    SchemaGenerationResult Schema,
    BusinessFunctionFactory FunctionFactory,
    object? CustomContext);

/// <summary>
/// Represents a previously executed tool invocation for custom tool selection logic.
/// </summary>
/// <param name="ToolName">Name of the tool that was executed.</param>
/// <param name="Summary">Short summary returned by the tool.</param>
public readonly record struct SchemaGuidedReasonerToolHistory(string ToolName, string Summary);

/// <summary>
/// Factory class encapsulating default configuration for SchemaGuidedReasoner.
/// </summary>
public static class SchemaGuidedReasonerFactory
{
    /// <summary>
    /// Creates a fully configured SchemaGuidedReasoner using the playground defaults.
    /// </summary>
    public static SchemaGuidedReasoner CreateDefault(
        Kernel kernel,
        DatabaseService databaseService,
        SqlServerService? sqlServerService = null)
    {
        if (kernel is null) throw new ArgumentNullException(nameof(kernel));
        if (databaseService is null) throw new ArgumentNullException(nameof(databaseService));

        var businessFunctionFactory = CreateDefaultBusinessFunctionFactory(databaseService, kernel, sqlServerService);
        var options = CreateDefaultOptions(databaseService);
        return new SchemaGuidedReasoner(kernel, businessFunctionFactory, options);
    }

    /// <summary>
    /// Builds the default BusinessFunctionFactory used by the playground scenario.
    /// </summary>
    /// <param name="databaseService">The database service instance</param>
    /// <param name="kernel">The Semantic Kernel instance (optional, creates minimal kernel if not provided)</param>
    /// <param name="sqlServerService">The SQL Server service instance (optional, creates new if not provided)</param>
    /// <returns>A configured BusinessFunctionFactory with default tools</returns>
    internal static BusinessFunctionFactory CreateDefaultBusinessFunctionFactory(
        DatabaseService databaseService,
        Kernel? kernel = null,
        SqlServerService? sqlServerService = null)
    {
        if (databaseService is null) throw new ArgumentNullException(nameof(databaseService));

        sqlServerService ??= new SqlServerService();
        var toolList = new Type[]
        {
            typeof(ReportTaskCompletionToolCall),
            typeof(SendEmailToolCall),
            typeof(IssueInvoiceToolCall),
            typeof(GetCustomerDataToolCall),
            typeof(VoidInvoiceToolCall),
            typeof(CreateRuleToolCall),
        };

        // Use provided kernel or create a minimal one
        // Note: A minimal kernel won't have LLM capabilities configured
        var factoryKernel = kernel ?? Kernel.CreateBuilder().Build();
        var loggerFactory = (factoryKernel.Services.GetService(typeof(ILoggerFactory)) as ILoggerFactory)
            ?? LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));

        return new BusinessFunctionFactory(databaseService, sqlServerService, factoryKernel, loggerFactory, toolList);
    }

    /// <summary>
    /// Builds default reasoner options capturing the existing business assistant persona.
    /// </summary>
    internal static SchemaGuidedReasonerOptions CreateDefaultOptions(DatabaseService databaseService)
    {
        if (databaseService is null) throw new ArgumentNullException(nameof(databaseService));

        var jsonOptions = SchemaGuidedReasonerOptions.CreateDefaultJsonOptions();

        var metadata = new DefaultPromptMetadata(
            Persona: "You are a business assistant helping Rinat Abdullin with customer interactions.",
            Guidelines: new[]
            {
                "Clearly report when tasks are done using report_task_completion",
                "Always send customers emails after issuing invoices (with invoice attached)",
                "Be laconic. Especially in emails",
                "No need to wait for payment confirmation before proceeding",
                "Always check customer data before issuing invoices or making changes",
                "When you determine that there is nothing to do anymore use the report_task_completion tool",
                "Try to send the email as last step after all other tasks are done"
            },
            ProductCatalogJson: databaseService.GetProductCatalogAsJson(jsonOptions));

        return new SchemaGuidedReasonerOptions
        {
            JsonSerializerOptions = jsonOptions,
            CustomContext = metadata,
            SystemPromptBuilder = (context, _) => BuildDefaultPrompt(context, metadata)
        };
    }

    private static string BuildDefaultPrompt(
        SchemaGuidedReasonerPromptContext context,
        DefaultPromptMetadata metadata)
    {
        var toolsSummary = SchemaGuidedReasoner.GenerateToolsSummary(context.Schema.AvailableTools);
        var guidelines = string.Join(Environment.NewLine, metadata.Guidelines.Select(g => $"- {g}"));

        return $@"
{metadata.Persona}

IMPORTANT: You must always respond with structured JSON that includes:
1. Current state analysis
2. List of remaining steps briefly described and include corresponding tool if applicable
3. Whether the task is completed
4. The specific tool call to execute next
5. The tool call to execute next is that one that logically follows from the current state and remaining steps

## Available Tools:
{toolsSummary}

Guidelines:
{guidelines}

Products: {metadata.ProductCatalogJson}";
    }

    private sealed record DefaultPromptMetadata(
        string Persona,
        IReadOnlyList<string> Guidelines,
        string ProductCatalogJson);
}
