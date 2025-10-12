using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Newtonsoft.Json;
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
            TypeInfoResolver = System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault
                ? new DefaultJsonTypeInfoResolver()
                : JsonTypeInfoResolver.Combine()
        };

        // Initialize the business function factory
        _functionFactory = new BusinessFunctionFactory(_databaseService);
    }

    private record ToolExecutionResult(string ToolName, string Summary);

    /// <summary>
    /// **Generates dynamic system prompt based on available tools**
    ///
    /// Creates a context-aware system prompt that includes only the tools
    /// that should be available for the current reasoning step.
    /// </summary>
    /// <param name="availableToolTypes">Types of tools that should be available for this request. If null, uses all tools.</param>
    /// <returns>Complete system prompt with tool documentation</returns>
    private string GenerateSystemPrompt(IEnumerable<Type>? availableToolTypes = null)
    {
        // Generate comprehensive schema documentation for available tools
        var schemaResult = availableToolTypes != null
            ? _functionFactory.GenerateSchemaWithDocumentationForToolCall(availableToolTypes)
            : _functionFactory.GenerateSchemaWithDocumentationForToolCall();

        return $@"
You are a business assistant helping Rinat Abdullin with customer interactions.

IMPORTANT: You must always respond with structured JSON that includes:
1. Current state analysis
2. List of remaining steps briefly described and include corresponding tool if applicable
3. Whether the task is completed
4. The specific tool call to execute next
5. The tool call to execute next is that one that logically follows from the current state and remaining steps

## Available Tools:
{GenerateToolsSummary(schemaResult.AvailableTools)}

Guidelines:
- Clearly report when tasks are done using report_task_completion
- Always send customers emails after issuing invoices (with invoice attached)
- Be laconic. Especially in emails
- No need to wait for payment confirmation before proceeding
- Always check customer data before issuing invoices or making changes
- When you determine that there is nothing to do anymore use the report_task_completion tool

Products: {_databaseService.GetProductCatalogAsJson(_jsonOptions)}";
    }

    /// <summary>
    /// **Determines which tools should be available for a given request context**
    ///
    /// This method can be extended in the future to implement sophisticated
    /// tool selection logic based on request context, user permissions,
    /// workflow state, or other business rules.
    /// </summary>
    /// <param name="userRequest">The user's request</param>
    /// <param name="executedTasks">Previously executed tasks in this session</param>
    /// <returns>Collection of tool types that should be available, or null for all tools</returns>
    private IEnumerable<Type>? DetermineAvailableTools(string userRequest, List<ToolExecutionResult> executedTasks)
    {
        // Future implementation could include sophisticated logic such as:
        // - Role-based tool access control
        // - Context-aware tool filtering
        // - Workflow state-based tool availability
        // - Security-based tool restrictions
        // - Dynamic tool loading based on request analysis

        // For now, return null to indicate all tools should be available
        // This maintains current behavior while enabling future customization
        return null;
    }

    /// <summary>
    /// **Generates a summary of available tools from tool information array**
    ///
    /// Creates a concise summary of available business tools for the LLM system prompt
    /// using the structured ToolInformation objects.
    /// </summary>
    /// <param name="availableTools">Array of ToolInformation objects with tool details</param>
    /// <returns>Formatted tools summary string</returns>
    private static string GenerateToolsSummary(ToolInformation[] availableTools)
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
            AnsiConsole.Write($"[yellow]Planning step_{step}...[/] ");

            try
            {
                // Make the LLM call explicit for this cycle
                AnsiConsole.MarkupLine($"[grey](LLM call #{step})[/]");

                // Force LLM to generate structured JSON response conforming to NextStep schema
                var (nextStep, assistantRaw) = await GetNextStepFromLLM(userRequest, executionTaskResult);

                // Show the raw assistant response (JSON) to aid debugging and transparency
                AnsiConsole.MarkupLine("[grey]Assistant raw response:[/]");
                AnsiConsole.WriteLine(Markup.Escape(string.IsNullOrWhiteSpace(assistantRaw)
                    ? "(empty response)"
                    : assistantRaw));

                // Display the full planned steps list returned by the LLM for this cycle
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

                // Display the single next action concisely as before
                var currentPlan = nextStep.PlanRemainingStepsBrief?.FirstOrDefault() ?? "No plan specified";
                AnsiConsole.MarkupLine($"[cyan]  → Next action: {Markup.Escape(currentPlan)}[/]");

                // Show which tool was selected and its parameters
                var toolName = nextStep.NextStepToolToCall?.GetType().Name.Replace("ToolCall", "") ?? "unknown";
                AnsiConsole.MarkupLine($"[green]  Selected tool:[/] {Markup.Escape(toolName)}");
                try
                {
                    var toolParamsJson = JsonConvert.SerializeObject(nextStep.NextStepToolToCall, Formatting.Indented);
                    AnsiConsole.WriteLine(Markup.Escape(toolParamsJson));
                }
                catch (Exception)
                {
                    // Fallback to type name if serialization fails
                    AnsiConsole.MarkupLine($"[grey]  (Could not serialize tool parameters; type: {toolName})[/]");
                }

                // Check if task is completed
                if (nextStep.NextStepToolToCall is ReportTaskCompletionToolCall completionParameter)
                {
                    AnsiConsole.MarkupLine($"[blue]Task completed: {Markup.Escape(completionParameter.Summary)}[/]");
                    return completionParameter.Summary;
                }

                // Ensure a tool was provided and dispatch it
                if (nextStep.NextStepToolToCall == null)
                {
                    throw new InvalidOperationException("LLM did not provide a NextStepToolToCall in the NextStep response.");
                }

                // Manually dispatch the tool function
                var businessResult = await _functionFactory.DispatchToolFunction(nextStep.NextStepToolToCall);

                // Add execution result to the list for next iteration
                var resultSummary = businessResult.Summary;
                executionTaskResult.Add(new ToolExecutionResult(toolName, resultSummary));

                // Use AnsiConsole.WriteLine instead of MarkupLine to avoid markup parsing issues
                AnsiConsole.Write("[green]    ✓ [/]");
                AnsiConsole.WriteLine(Markup.Escape(resultSummary));
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