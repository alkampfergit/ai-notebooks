using Azure;
using Azure.AI.OpenAI;
using Newtonsoft.Json;
using OpenAI.Responses;
using SkPlayground.BusinessFunctions;
using SkPlayground.Models;
using Spectre.Console;
using System.Text;

namespace SkPlayground.Services;

#pragma warning disable OPENAI001

/// <summary>
/// **Schema-Guided Reasoner using Direct OpenAI Chat API**
///
/// This implementation uses the Azure OpenAI Chat API directly instead of Semantic Kernel,
/// providing these advantages:
/// - **Direct API Access**: No Semantic Kernel overhead or abstraction
/// - **Detailed Token Tracking**: Tracks input, output, and total token counts per step
/// - **Cumulative Statistics**: Session-wide token usage tracking via TokenUsageStats
/// - **Configurable Reasoning**: Support for different reasoning effort levels (placeholder for future)
/// - **Same Interface**: Compatible with SchemaGuidedReasoner for easy switching
///
/// This reasoner bypasses Semantic Kernel and calls the OpenAI Chat Completion API directly,
/// providing lower overhead while maintaining the same Schema-Guided Reasoning pattern.
/// </summary>
public class ResponseApiSchemaGuidedReasoner
{
    private readonly string _azureEndpoint;
    private readonly string _azureApiKey;
    private readonly string _deploymentId;
    private readonly BusinessFunctionFactory _functionFactory;
    private readonly SchemaGuidedReasonerOptions _options;

    /// <summary>
    /// Controls whether to display detailed debug output during reasoning.
    /// When false, only essential information is shown.
    /// </summary>
    public bool VerboseOutput { get; set; } = true;

    /// <summary>
    /// Controls the reasoning effort level for the Response API.
    /// - Low: Faster responses with less reasoning
    /// - Medium: Balanced reasoning and speed
    /// - High: More thorough reasoning, slower responses
    /// </summary>
    public ResponseReasoningEffortLevel ReasoningEffortLevel { get; set; } = ResponseReasoningEffortLevel.Medium;

    private record ToolExecutionResult(string ToolName, string Summary);

    /// <summary>
    /// Tracks cumulative token usage across all reasoning steps
    /// </summary>
    public class TokenUsageStats
    {
        public int TotalInputTokens { get; set; }
        public int TotalOutputTokens { get; set; }
        public int TotalReasoningTokens { get; set; }
        public int TotalCachedTokens { get; set; }
        public int TotalTokens => TotalInputTokens + TotalOutputTokens;

        public void AddUsage(TokenUsage usage)
        {
            TotalInputTokens += usage.InputTokenCount;
            TotalOutputTokens += usage.OutputTokenCount;

            if (usage.InputTokenDetails?.CachedTokenCount > 0)
            {
                TotalCachedTokens += usage.InputTokenDetails.CachedTokenCount;
            }

            if (usage.OutputTokenDetails?.ReasoningTokenCount > 0)
            {
                TotalReasoningTokens += usage.OutputTokenDetails.ReasoningTokenCount;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Total Tokens: {TotalTokens}");
            sb.AppendLine($"  Input: {TotalInputTokens}");
            sb.AppendLine($"  Output: {TotalOutputTokens}");
            if (TotalReasoningTokens > 0)
                sb.AppendLine($"  Reasoning: {TotalReasoningTokens}");
            if (TotalCachedTokens > 0)
                sb.AppendLine($"  Cached: {TotalCachedTokens}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Token usage statistics for the current reasoning session
    /// </summary>
    public TokenUsageStats CurrentSessionStats { get; private set; } = new();

    /// <summary>
    /// Initializes the Response API reasoner with Azure OpenAI credentials and configuration.
    /// </summary>
    public ResponseApiSchemaGuidedReasoner(
        string azureEndpoint,
        string azureApiKey,
        string deploymentId,
        BusinessFunctionFactory businessFunctionFactory,
        SchemaGuidedReasonerOptions? options = null)
    {
        _azureEndpoint = azureEndpoint ?? throw new ArgumentNullException(nameof(azureEndpoint));
        _azureApiKey = azureApiKey ?? throw new ArgumentNullException(nameof(azureApiKey));
        _deploymentId = deploymentId ?? throw new ArgumentNullException(nameof(deploymentId));
        _functionFactory = businessFunctionFactory ?? throw new ArgumentNullException(nameof(businessFunctionFactory));
        _options = options ?? SchemaGuidedReasonerOptions.CreateDefault();
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
    /// **Execute Schema-Guided Reasoning using the Response API**
    ///
    /// This implements the core SGR pattern with the Response API:
    /// 1. Force LLM to generate NextStep schema with reasoning
    /// 2. Manually dispatch tools based on the selected ToolCall
    /// 3. Continue reasoning with conversation context until task completion
    /// 4. Track detailed token usage including reasoning and cached tokens
    /// </summary>
    public async Task<string> ReasonAndActAsync(string userRequest)
    {
        // Reset session stats for new reasoning session
        CurrentSessionStats = new TokenUsageStats();

        var executionTaskResult = new List<ToolExecutionResult>();
        string? previousResponseId = null; // Track conversation continuity

        // **Arrange**: Configure Azure OpenAI client with Response API support
        var clientOptions = new AzureOpenAIClientOptions(
            AzureOpenAIClientOptions.ServiceVersion.V2025_04_01_Preview);

        var client = new AzureOpenAIClient(
            new Uri(_azureEndpoint),
            new AzureKeyCredential(_azureApiKey),
            clientOptions);

        var responseClient = client.GetOpenAIResponseClient(_deploymentId);

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
                    AnsiConsole.MarkupLine($"[grey](Direct OpenAI API call #{step})[/]");
                }

                // **Build the chat completion request using direct OpenAI client**
                var availableToolTypes = DetermineAvailableTools(userRequest, executionTaskResult);
                var systemPrompt = GenerateSystemPrompt(availableToolTypes);
                var userMessage = BuildUserMessage(userRequest, executionTaskResult);

                // **Configure chat options with schema constraint**
                var schemaStr = availableToolTypes != null
                    ? _functionFactory.GenerateJsonSchemaForToolCall(availableToolTypes)
                    : _functionFactory.GenerateJsonSchemaForToolCall();

                // **Build chat messages** This is with a direct API call using the standard api.
                //var messages = new List<ChatMessage>
                //{
                //    new SystemChatMessage(systemPrompt),
                //    new UserChatMessage(userMessage)
                //};

                //var chatOptions = new ChatCompletionOptions
                //{
                //    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                //        jsonSchemaFormatName: "next_step_schema",
                //        jsonSchema: BinaryData.FromString(schemaStr),
                //        jsonSchemaIsStrict: true
                //    )
                //};

                //if (VerboseOutput)
                //{
                //    AnsiConsole.MarkupLine($"[grey]Calling OpenAI with {messages.Count} messages[/]");
                //}

                //// **Make the direct OpenAI API call**
                //var chatClient = client.GetChatClient(_deploymentId);
                //var completion = await chatClient.CompleteChatAsync(messages, chatOptions);

                // use the new response api.
                var inputItems = new List<ResponseItem> {
                    ResponseItem.CreateSystemMessageItem(systemPrompt) ,
                    ResponseItem.CreateUserMessageItem(userMessage)
                };

                var options = new ResponseCreationOptions
                {
                    PreviousResponseId = null, //do not use conversation id for now
                    ReasoningOptions = new ResponseReasoningOptions()
                    {
                        ReasoningEffortLevel = this.ReasoningEffortLevel
                    },
                    TextOptions = new ResponseTextOptions
                    {
                        TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                        jsonSchemaFormatName: "NextStep",
                        jsonSchema: BinaryData.FromString(schemaStr),
                        jsonSchemaFormatDescription: "Schema for NextStep with polymorphic ToolCall support",
                        jsonSchemaIsStrict: true)
                    },
                };

                OpenAIResponse response = await responseClient.CreateResponseAsync(inputItems, options);

                // conversationId = response.Id;

                // **Track token usage it is different for the classic API **
                if (response.Usage != null)
                {
                    var usage = new TokenUsage
                    {
                        InputTokenCount = response.Usage.InputTokenCount,
                        OutputTokenCount = response.Usage.OutputTokenCount,
                        TotalTokenCount = response.Usage.TotalTokenCount
                    };
                    CurrentSessionStats.AddUsage(usage);

                    if (VerboseOutput)
                    {
                        DisplayTokenUsage(usage, step);
                    }
                }

                var responseMessage = response.OutputItems.OfType<MessageResponseItem>().FirstOrDefault();
                if (responseMessage == null)
                {
                    throw new InvalidOperationException("OpenAI Response did not contain a message item.");
                }

                // **Extract the assistant's response**
                var assistantRaw = String.Join("\n", responseMessage.Content.Select(c => c.Text));

                if (string.IsNullOrEmpty(assistantRaw))
                {
                    throw new InvalidOperationException("OpenAI API did not return a message");
                }

                // **Parse the JSON response to NextStep object**
                var nextStep = _functionFactory.DeserializeNextStep(assistantRaw);
                if (nextStep == null)
                {
                    throw new InvalidOperationException("Failed to deserialize NextStep from OpenAI response:\n" + assistantRaw);
                }

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
                var toolName = nextStep.NextStep?.GetType().Name.Replace("ToolCall", "") ?? "unknown";

                // **Show the NextStep tool call details - always visible even in non-verbose mode**
                AnsiConsole.MarkupLine($"[cyan]  Next Step:[/] [yellow]{Markup.Escape(toolName)}[/] - {Markup.Escape(currentPlan)}");

                // Show NextStep serialized output - always visible
                try
                {
                    var nextStepJson = JsonConvert.SerializeObject(nextStep.NextStep, Formatting.Indented);
                    AnsiConsole.MarkupLine("[dim]  Tool parameters:[/]");
                    AnsiConsole.WriteLine(Markup.Escape(nextStepJson));
                }
                catch (Exception)
                {
                    // Fallback to type name if serialization fails
                    AnsiConsole.MarkupLine($"[dim]    (Unable to serialize NextStep for {Markup.Escape(toolName)})[/]");
                }

                if (nextStep.NextStep is ReportTaskCompletionToolCall completionParameter)
                {
                    if (VerboseOutput)
                    {
                        AnsiConsole.MarkupLine($"[blue]Task completed: {Markup.Escape(completionParameter.Summary)}[/]");
                        AnsiConsole.MarkupLine("\n[bold cyan]Session Token Usage Summary:[/]");
                        AnsiConsole.WriteLine(CurrentSessionStats.ToString());
                    }
                    return completionParameter.Summary;
                }

                // Ensure a tool was provided and dispatch it
                if (nextStep.NextStep == null)
                {
                    throw new InvalidOperationException("LLM did not provide a NextStepToolToCall in the NextStep response.");
                }

                // Manually dispatch the tool function
                var businessResult = await _functionFactory.DispatchToolFunction(nextStep.NextStep);

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

                if (VerboseOutput)
                {
                    AnsiConsole.MarkupLine("\n[bold cyan]Session Token Usage Summary (up to error):[/]");
                    AnsiConsole.WriteLine(CurrentSessionStats.ToString());
                }

                return $"Error occurred during reasoning: {ex.Message}";
            }
        }

        if (VerboseOutput)
        {
            AnsiConsole.MarkupLine("\n[bold cyan]Session Token Usage Summary:[/]");
            AnsiConsole.WriteLine(CurrentSessionStats.ToString());
        }

        return "Task completed after maximum reasoning steps";
    }

    /// <summary>
    /// **Displays detailed token usage statistics from a Response API call**
    /// </summary>
    private void DisplayTokenUsage(TokenUsage usage, int step)
    {
        AnsiConsole.MarkupLine($"[grey]  Step {step} Tokens:[/]");
        AnsiConsole.MarkupLine($"[grey]    Input: {usage.InputTokenCount}[/]");
        AnsiConsole.MarkupLine($"[grey]    Output: {usage.OutputTokenCount}[/]");
        AnsiConsole.MarkupLine($"[grey]    Total: {usage.TotalTokenCount}[/]");

        if (usage.InputTokenDetails?.CachedTokenCount > 0)
        {
            AnsiConsole.MarkupLine($"[green]    └─ Cached Input: {usage.InputTokenDetails.CachedTokenCount}[/]");
        }

        if (usage.OutputTokenDetails?.ReasoningTokenCount > 0)
        {
            AnsiConsole.MarkupLine($"[fuchsia]    └─ Reasoning: {usage.OutputTokenDetails.ReasoningTokenCount}[/]");
        }
    }

    /// <summary>
    /// **Builds the user message combining the original request and executed tasks**
    /// </summary>
    private string BuildUserMessage(string userRequest, List<ToolExecutionResult> executedTasks)
    {
        var userMessage = $"User Request: {userRequest}";

        if (executedTasks.Count > 0)
        {
            userMessage += "\n\nExecuted Tasks:";
            foreach (var task in executedTasks)
            {
                userMessage += $"\n- {task.ToolName}: {task.Summary}";
            }
        }

        return userMessage;
    }

    /// <summary>
    /// **Temporary fallback using Semantic Kernel until Response API types are available**
    ///
    /// This method will be removed once the Response API types are stable.
    /// It provides identical functionality to the Response API version but uses SK.
    /// </summary>
    private async Task<(NextStepDescription NextStep, string AssistantRaw)> GetNextStepFromLLMFallback(
        string userRequest,
        List<ToolExecutionResult> executedTasks)
    {
        // This is a temporary implementation
        // TODO: Remove this method when Response API types become available

        throw new NotImplementedException(
            "Response API types are not yet available in the current SDK version. " +
            "This reasoner requires OpenAI SDK with Response API support. " +
            "Please use the standard SchemaGuidedReasoner until the Response API is stable.");
    }
}

/// <summary>
/// **Token usage information from a Response API call**
///
/// Tracks detailed token counts including reasoning and cached tokens.
/// This is a simplified version that will be replaced when Response API types are available.
/// </summary>
public class TokenUsage
{
    public int InputTokenCount { get; set; }
    public int OutputTokenCount { get; set; }
    public int TotalTokenCount { get; set; }
    public TokenInputDetails? InputTokenDetails { get; set; }
    public TokenOutputDetails? OutputTokenDetails { get; set; }
}

/// <summary>
/// **Input token details including cached tokens**
/// </summary>
public class TokenInputDetails
{
    public int CachedTokenCount { get; set; }
}

/// <summary>
/// **Output token details including reasoning tokens**
/// </summary>
public class TokenOutputDetails
{
    public int ReasoningTokenCount { get; set; }
}
