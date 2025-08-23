using System.Text.Json;
using Microsoft.SemanticKernel;
using SkPlayground.Models;

namespace SkPlayground.Services;

public class SchemaGuidedReasoner
{
    private readonly Kernel _kernel;
    private readonly ToolDispatcher _dispatcher;
    private readonly List<string> _conversationLog;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public SchemaGuidedReasoner(Kernel kernel, ToolDispatcher dispatcher, JsonSerializerOptions jsonOptions)
    {
        _kernel = kernel;
        _dispatcher = dispatcher;
        _conversationLog = new List<string>();
        _jsonOptions = jsonOptions;
    }
    
    public async Task<string> ReasonAndActAsync(string userRequest)
    {
        _conversationLog.Add($"User: {userRequest}");
        
        var systemPrompt = """
        You are an AI assistant that follows a Schema-Guided Reasoning approach. 
        
        You must ALWAYS respond with a JSON object matching the NextStep schema:
        {
          "current_state": "string describing what you understand about the current situation",
          "plan_remaining_steps_brief": ["step1", "step2", "..."] (1-5 brief steps),
          "task_completed": boolean,
          "function": {
            "tool": "tool_name",
            "...": "tool-specific parameters"
          }
        }
        
        Available tools:
        - report_task_completion: {"tool": "report_task_completion", "summary": "string"}
        - send_email: {"tool": "send_email", "to": "email", "subject": "string", "body": "string"}
        - issue_invoice: {"tool": "issue_invoice", "email": "email", "skus": ["SKU1", "SKU2"], "discount_percent": 0-50}
        - query_database: {"tool": "query_database", "query": "string"}
        - update_database: {"tool": "update_database", "table": "string", "updates": {}, "where_clause": "string"}
        
        Think step by step, break down complex tasks, and use the appropriate tools.
        """;
        
        var conversationContext = string.Join("\n", _conversationLog);
        var fullPrompt = $"{systemPrompt}\n\nConversation so far:\n{conversationContext}\n\nRespond with NextStep JSON:";
        
        try
        {
            var result = await _kernel.InvokePromptAsync(fullPrompt);
            var responseText = result.ToString().Trim();
            
            // Clean up response (remove markdown code blocks if present)
            if (responseText.StartsWith("```json"))
            {
                responseText = responseText.Substring(7);
            }
            if (responseText.EndsWith("```"))
            {
                responseText = responseText.Substring(0, responseText.Length - 3);
            }
            responseText = responseText.Trim();
            
            Console.WriteLine($"🤖 LLM Response: {responseText}");
            
            // Parse the NextStep
            var nextStep = JsonSerializer.Deserialize<NextStep>(responseText, _jsonOptions);
            
            if (nextStep == null)
            {
                throw new InvalidOperationException("Failed to parse NextStep from LLM response");
            }
            
            // Log the reasoning
            _conversationLog.Add($"AI State: {nextStep.CurrentState}");
            _conversationLog.Add($"AI Plan: {string.Join(", ", nextStep.PlanRemainingStepsBrief)}");
            
            // Execute the function
            var toolResult = await _dispatcher.DispatchAsync(nextStep.Function);
            _conversationLog.Add($"Tool Result: {toolResult}");
            
            return toolResult;
        }
        catch (JsonException ex)
        {
            var errorMsg = $"JSON parsing error: {ex.Message}";
            _conversationLog.Add($"Error: {errorMsg}");
            return errorMsg;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error: {ex.Message}";
            _conversationLog.Add($"Error: {errorMsg}");
            return errorMsg;
        }
    }
    
    public void PrintConversationLog()
    {
        Console.WriteLine("\n📝 Conversation Log:");
        Console.WriteLine("==================");
        foreach (var entry in _conversationLog)
        {
            Console.WriteLine(entry);
        }
        Console.WriteLine("==================\n");
    }
}