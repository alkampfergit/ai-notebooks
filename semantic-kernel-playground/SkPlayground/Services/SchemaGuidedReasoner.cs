using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkPlayground.Models;
using SkPlayground.BusinessFunctions;
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
    private readonly List<ChatMessageContent> _conversationHistory;
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
            TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault 
                ? new DefaultJsonTypeInfoResolver() 
                : JsonTypeInfoResolver.Combine()
        };

        // Initialize the business function factory
        _functionFactory = new BusinessFunctionFactory(_jsonOptions, _databaseService);
        
        // Initialize conversation history with system prompt for structured JSON responses
        var systemPrompt = $@"
You are a business assistant helping Rinat Abdullin with customer interactions.

IMPORTANT: You must always respond with structured JSON that includes:
1. Current state analysis
2. List of remaining steps briefly described
3. Whether the task is completed
4. The specific tool call to execute next (with proper type discriminator)

Available function types:
- send_email: Send emails to customers
- issue_invoice: Create invoices for customers  
- get_customer_data: Retrieve customer information
- void_invoice: Cancel existing invoices
- create_rule: Create business rules for customers
- report_task_completion: Complete tasks with summary

Guidelines:
- Clearly report when tasks are done using report_task_completion
- Always send customers emails after issuing invoices (with invoice attached)
- Be laconic. Especially in emails
- No need to wait for payment confirmation before proceeding
- Always check customer data before issuing invoices or making changes

Products: {_databaseService.GetProductCatalogAsJson(_jsonOptions)}";
        
        _conversationHistory = [new ChatMessageContent(AuthorRole.System, systemPrompt)];
    }

    /// <summary>
    /// Execute Schema-Guided Reasoning for the given user request.
    /// This implements the core SGR pattern: force LLM to generate NextStep schema,
    /// manually dispatch tools, and continue reasoning until task completion.
    /// </summary>
    public async Task<string> ReasonAndActAsync(string userRequest)
    {
        // Add user request to conversation history
        _conversationHistory.Add(new(AuthorRole.User, userRequest));
        
        // Limit reasoning steps to prevent infinite loops (matching Python original)
        for (int step = 1; step <= 20; step++)
        {
            AnsiConsole.Write($"[yellow]Planning step_{step}...[/] ");
            
            try
            {
                // Force LLM to generate structured JSON response conforming to NextStep schema
                var llmResponse = await GetNextStepFromLLM();
                var nextStepResults = llmResponse.NextStepResults;

                // Check if no steps were generated
                if (nextStepResults.Length == 0)
                {
                    AnsiConsole.MarkupLine("[red]No next step generated by LLM[/]");
                    return "Error: No next step generated by LLM";
                }
                
                // Display the number of steps to be executed
                if (nextStepResults.Length > 1)
                {
                    AnsiConsole.MarkupLine($"[cyan]Executing {nextStepResults.Length} tasks in parallel[/]");
                }

                // Add the assistant's response from the actual LLM call to conversation history
                // This response contains the proper function call structure matching OpenAI format
                _conversationHistory.Add(llmResponse.AssistantResponse);
                
                // Execute all tasks and collect their results
                var allToolMessages = new List<ChatMessageContent>();
                string? taskCompletionSummary = null;
                
                foreach (var nextStepResult in nextStepResults)
                {
                    var nextStep = nextStepResult.NextStep;
                    var functionName = nextStepResult.FunctionName;
                    
                    // Display the planned step
                    var currentPlan = nextStep.PlanRemainingStepsBrief?.FirstOrDefault() ?? "No plan specified";
                    AnsiConsole.MarkupLine($"[cyan]  → {currentPlan}[/]");
                    AnsiConsole.MarkupLine($"[dim]    Function: {functionName}[/]");
                    
                    // Check if task is completed
                    if (nextStep.ToolCall is ReportTaskCompletionToolCall completionParameter)
                    {
                        AnsiConsole.MarkupLine($"[blue]Task completed[/]");
                        taskCompletionSummary = completionParameter.Summary;
                        
                        // Add completion result as user message for conversation history
                        var completionMessage = new ChatMessageContent(
                            role: AuthorRole.User, 
                            content: $"Task completed: {completionParameter.Summary}"
                        );
                        allToolMessages.Add(completionMessage);
                        continue;
                    }
                    
                    // Manually dispatch the tool function
                    var businessResult = await _functionFactory.DispatchToolFunction(nextStep);
                    
                    // Use the summary for LLM conversation, not the full result object
                    var resultSummary = businessResult.Summary;

                    // Add tool result to conversation history as user message
                    var toolMessage = new ChatMessageContent(
                        role: AuthorRole.User,
                        content: $"Function {functionName} result: {resultSummary}"
                    );
                    allToolMessages.Add(toolMessage);
                    
                    // Use AnsiConsole.WriteLine instead of MarkupLine to avoid markup parsing issues
                    AnsiConsole.Write("[green]    ✓ [/]");
                    AnsiConsole.WriteLine(resultSummary);
                }
                
                // Add all tool messages to conversation history
                foreach (var toolMessage in allToolMessages)
                {
                    _conversationHistory.Add(toolMessage);
                }
                
                // If any task was completed, return the completion summary
                if (taskCompletionSummary != null)
                {
                    return taskCompletionSummary;
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
    /// Returns both the NextStep objects and the original assistant response.
    /// </summary>
    private async Task<LLMReasoningResponse> GetNextStepFromLLM()
    {
        var chatHistory = new ChatHistory();
        foreach (var message in _conversationHistory)
        {
            chatHistory.Add(message);
        }


        // Configure OpenAI execution settings with JSON schema constraint
        // Serialize schema to string and embed it into a "json_schema" response_format object
        var schemaStr = _functionFactory.GenerateJsonSchemaForToolCall();
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = new
            {
                type = "json_schema",
                json_schema = schemaStr
            }
        };
    
        
        var response = await _chatService.GetChatMessageContentAsync(chatHistory, executionSettings);
        
        // Parse the JSON response to NextStep object
        var jsonContent = response.Content ?? string.Empty;
        NextStep? nextStep = null;
        
        try
        {
            nextStep = JsonSerializer.Deserialize<NextStep>(jsonContent, _jsonOptions);
        }
        catch (JsonException ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to parse NextStep JSON: {ex.Message}[/]");
            AnsiConsole.MarkupLine($"[dim]Response content: {jsonContent}[/]");
        }

        // Determine function name from ToolCall discriminator
        var functionName = GetFunctionNameFromToolCall(nextStep?.ToolCall);
        
        var nextStepResults = nextStep != null && !string.IsNullOrEmpty(functionName)
            ? new[] { new NextStepResult(nextStep, functionName) }
            : Array.Empty<NextStepResult>();

        return new LLMReasoningResponse(nextStepResults, response);
    }

    /// <summary>
    /// **Maps ToolCall types to their corresponding function names** based on type discriminators.
    /// 
    /// This method determines which business function should be called based on the
    /// specific ToolCall type returned in the NextStep schema.
    /// </summary>
    /// <param name="toolCall">The ToolCall object from the LLM response</param>
    /// <returns>Function name string, or null if ToolCall type is not recognized</returns>
    private static string? GetFunctionNameFromToolCall(ToolCall? toolCall)
    {
        return toolCall switch
        {
            SendEmailToolCall => "sendEmail",
            IssueInvoiceToolCall => "issueInvoice",
            GetCustomerDataToolCall => "getCustomerData",
            VoidInvoiceToolCall => "voidInvoice",
            CreateRuleToolCall => "createRule",
            ReportTaskCompletionToolCall => "reportTaskCompletion",
            _ => null
        };
    }
    
    // /// <summary>
    // /// Manually dispatch tool function calls based on the NextStep schema.
    // /// This replaces Semantic Kernel's automatic tool calling with explicit dispatch.
    // /// </summary>
    // private Task<object> DispatchToolFunction(ToolFunction toolFunction)
    // {
    //     var result = toolFunction switch
    //     {
    //         SendEmail email => _toolHandler.HandleSendEmail(email.RecipientEmail, email.Subject, email.Message),
    //         IssueInvoice invoice => _toolHandler.HandleIssueInvoice(invoice.Email, [.. invoice.Skus], invoice.DiscountPercent),
    //         GetCustomerData customer => _toolHandler.HandleGetCustomerData(customer.Email),
    //         VoidInvoice voidInv => _toolHandler.HandleVoidInvoice(voidInv.InvoiceId, voidInv.Reason),
    //         CreateRule rule => _toolHandler.HandleCreateRule(rule.Email, rule.Rule),
    //         ReportTaskCompletion completion => _toolHandler.HandleTaskCompletion(completion.Summary),
    //         _ => throw new InvalidOperationException($"Unknown tool function: {toolFunction.GetType().Name}")
    //     };
    //     return Task.FromResult(result);
    // }

    /// <summary>
    /// Display the complete conversation log showing the SGR reasoning process
    /// </summary>
    public void PrintConversationLog()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Spectre.Console.Rule("[bold blue]Schema-Guided Reasoning Conversation Log[/]")
                .RuleStyle("blue"));
        
        foreach (var entry in _conversationHistory)
        {
            var roleColor = entry.Role.ToString().ToLower() switch
            {
                "system" => "grey",
                "user" => "green", 
                "assistant" => "blue",
                "tool" => "yellow",
                _ => "white"
            };
            
            AnsiConsole.MarkupLine($"[{roleColor}]{entry.Role}:[/] {entry.Content}");
        }
        
        AnsiConsole.Write(
            new Spectre.Console.Rule("[dim]End of Conversation Log[/]")
                .RuleStyle("grey"));
        AnsiConsole.WriteLine();
    }
}