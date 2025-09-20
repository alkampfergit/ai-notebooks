using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkPlayground.Models;
using SkPlayground.BusinessFunctions;
using SkPlayground.Utils;
using Spectre.Console;

namespace SkPlayground.Services;

/// <summary>
/// Represents the result of a reasoning step containing both the NextStep object and function name.
/// </summary>
/// <param name="NextStep">The deserialized NextStep object containing the reasoning parameters and tool call</param>
/// <param name="FunctionName">The name of the function that the LLM chose to call (derived from ToolCall discriminator)</param>
public readonly record struct NextStepResult(NextStep NextStep, string FunctionName);

/// <summary>
/// Represents the complete LLM response including both parsed results and the original assistant message
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
    private readonly DatabaseService _databaseService;
    private readonly BusinessFunctionFactory _functionFactory;
    private readonly string _systemPrompt;

    /// <summary>
    /// Initialize the Schema-Guided Reasoner with the kernel and database service
    /// </summary>
    public SchemaGuidedReasoner(Kernel kernel, DatabaseService databaseService)
    {
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _databaseService = databaseService;

        // Configure JSON serialization options for NextStep schema parsing
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault
                ? new DefaultJsonTypeInfoResolver()
                : JsonTypeInfoResolver.Combine()
        };

        // Initialize the business function factory
        _functionFactory = new BusinessFunctionFactory(_databaseService);

        // Generate comprehensive schema documentation
        var schemaResult = _functionFactory.GenerateSchemaWithDocumentationForToolCall();

        // Initialize conversation history with system prompt for structured JSON responses
        _systemPrompt = $@"
You are a business assistant helping Rinat Abdullin with customer interactions.

IMPORTANT: You must always respond with structured JSON that includes:
1. Current state analysis
2. List of remaining steps briefly described
3. Whether the task is completed
4. The specific tool call to execute next (with proper type discriminator)

## Tool Description:
{schemaResult.ToolDescription}

## Available Functions:
{GenerateFunctionSummary(schemaResult)}

## Property Documentation:
{schemaResult.PropertyDescriptions}

Guidelines:
- Clearly report when tasks are done using report_task_completion
- Always send customers emails after issuing invoices (with invoice attached)
- Be laconic. Especially in emails
- No need to wait for payment confirmation before proceeding
- Always check customer data before issuing invoices or making changes
- When you determine that there is nothing to do anymore use the report_task_completion tool

Products: {_databaseService.GetProductCatalogAsJson(_jsonOptions)}";
    }

    private record ToolExecutionResult(string ToolName, string Summary);

    /// <summary>
    /// **Generates a summary of available functions from schema documentation**
    ///
    /// Extracts function information from the schema result to create a concise
    /// summary of available business functions for the LLM system prompt.
    /// </summary>
    /// <param name="schemaResult">The comprehensive schema result with documentation</param>
    /// <returns>Formatted function summary string</returns>
    private static string GenerateFunctionSummary(SchemaGenerationResult schemaResult)
    {
        var summary = new StringBuilder();

        // Extract function types from the schema by looking at the JSON definitions
        var schemaNode = JsonNode.Parse(schemaResult.JsonSchema);
        var definitions = schemaNode?["definitions"]?.AsObject();

        if (definitions != null)
        {
            foreach (var definition in definitions)
            {
                var functionName = definition.Key;
                var functionSchema = definition.Value?.AsObject();

                // Get description from the schema if available
                var description = functionSchema?["description"]?.ToString() ??
                                 GetFunctionDescriptionFromName(functionName);

                // Convert from PascalCase to readable format
                var readableName = ConvertToReadableName(functionName);

                summary.AppendLine($"- **{readableName}**: {description}");
            }
        }

        return summary.ToString();
    }

    /// <summary>
    /// **Converts PascalCase function names to readable format**
    /// </summary>
    private static string ConvertToReadableName(string functionName)
    {
        // Remove "ToolCall" suffix if present
        var cleanName = functionName.EndsWith("ToolCall")
            ? functionName[..^8]
            : functionName;

        // Convert PascalCase to space-separated words
        return System.Text.RegularExpressions.Regex.Replace(cleanName,
            "([a-z])([A-Z])", "$1 $2").ToLowerInvariant();
    }

    /// <summary>
    /// **Provides default descriptions for function names**
    /// </summary>
    private static string GetFunctionDescriptionFromName(string functionName)
    {
        return functionName.ToLowerInvariant() switch
        {
            var name when name.Contains("email") => "Send emails to customers",
            var name when name.Contains("invoice") => "Create invoices for customers",
            var name when name.Contains("customer") => "Retrieve customer information",
            var name when name.Contains("void") => "Cancel existing invoices",
            var name when name.Contains("rule") => "Create business rules",
            var name when name.Contains("completion") => "Complete tasks with summary",
            _ => "Business operation function"
        };
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
            AnsiConsole.Write($"[yellow]Planning step_{step}...[/] ");

            try
            {
                // Force LLM to generate structured JSON response conforming to NextStep schema
                var nextStep = await GetNextStepFromLLM(userRequest, executionTaskResult);

                // Display the planned step
                var currentPlan = nextStep.PlanRemainingStepsBrief?.FirstOrDefault() ?? "No plan specified";
                AnsiConsole.MarkupLine($"[cyan]  → {currentPlan}[/]");

                // Check if task is completed
                if (nextStep.ToolCall is ReportTaskCompletionToolCall completionParameter)
                {
                    AnsiConsole.MarkupLine($"[blue]Task completed: {completionParameter.Summary}[/]");
                    return completionParameter.Summary;
                }

                // Manually dispatch the tool function
                var businessResult = await _functionFactory.DispatchToolFunction(nextStep.ToolCall);

                // Add execution result to the list for next iteration
                var resultSummary = businessResult.Summary;
                var toolName = nextStep.ToolCall.GetType().Name.Replace("ToolCall", "");
                executionTaskResult.Add(new ToolExecutionResult(toolName, resultSummary));
           

                // Use AnsiConsole.WriteLine instead of MarkupLine to avoid markup parsing issues
                AnsiConsole.Write("[green]    ✓ [/]");
                AnsiConsole.WriteLine(resultSummary);
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
    private async Task<NextStep> GetNextStepFromLLM(string userRequest, List<ToolExecutionResult> executedTasks)
    {
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
        chatHistory.AddSystemMessage(_systemPrompt);
        chatHistory.AddUserMessage(userMessage);

        // Configure OpenAI execution settings with JSON schema constraint
        // Serialize schema to string and embed it into a "json_schema" response_format object
        var schemaStr = _functionFactory.GenerateJsonSchemaForToolCall();
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

        return _functionFactory.DeserializeNextStep(jsonContent);
    }
}