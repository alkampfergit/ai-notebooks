using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkPlayground.Models;

namespace SkPlayground.Services;

public class SchemaGuidedReasoner
{
    private readonly Kernel _kernel;
    private readonly ChatHistory _chatHistory;
    private readonly OpenAIPromptExecutionSettings _executionSettings;

    private readonly ToolHandler _toolHandler;
    public SchemaGuidedReasoner(Kernel kernel, ToolHandler toolHandler)
    {
        _kernel = kernel;
        _chatHistory = new ChatHistory($@"
You are a business assistant helping Rinat Abdullin with customer interactions.
- Clearly report when tasks are done.
- Always send customers emails after issuing invoices (with invoice attached).
- Be laconic. Especially in emails
- No need to wait for payment confirmation before proceeding.
- Always check customer data before issuing invoices or making changes.
Products: {toolHandler.GetDbAsJson()}");
        _toolHandler = toolHandler;

        // Define tools as KernelFunctions using lambdas
        var reportTaskCompletion = KernelFunctionFactory.CreateFromMethod(
                [Description("Call this to report the final result of a task to the user.")] (
                [Description("A summary of the task outcome.")] string summary
            ) =>
                {
                    return _toolHandler.HandleTaskCompletion(summary);
                },
            "report_task_completion");

        var sendEmail = KernelFunctionFactory.CreateFromMethod(
            [Description("Sends an email.")] (
                [Description("Recipient's email address.")] string to,
                [Description("Email subject.")] string subject,
                [Description("Email body.")] string body
            ) =>
            {
                return _toolHandler.HandleSendEmail(to, subject, body);
            },
            "send_email");

        var issueInvoice = KernelFunctionFactory.CreateFromMethod(
            [Description("Issues an invoice to a customer.")] (
                [Description("Recipient's email address.")] string email,
                [Description("List of product SKUs.")] string[] skus,
                [Description("Discount percentage (0-50).")] double discount_percent
            ) =>
            {
                return _toolHandler.HandleIssueInvoice(email, skus, discount_percent);
            },
            "issue_invoice");

        var queryDatabase = KernelFunctionFactory.CreateFromMethod(
            [Description("Queries the company database.")] (
                [Description("The database query to execute.")] string query
            ) =>
            {
                return _toolHandler.HandleQueryDatabase(query);
            },
            "query_database");

        var updateDatabase = KernelFunctionFactory.CreateFromMethod(
            [Description("Updates a record in the company database.")] (
                [Description("The table to update.")] string table,
                [Description("An object with key-value pairs for the update.")] object updates,
                [Description("The WHERE clause to select the record to update.")] string where_clause
            ) =>
            {
                return _toolHandler.HandleUpdateDatabase(table, updates, where_clause);
            },
            "update_database");

        _executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };

        // Add tools to the kernel so they can be used by the planner
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions("Tools", "Tools available to the AI", 
        [
            reportTaskCompletion,
            sendEmail,
            issueInvoice,
            queryDatabase,
            updateDatabase
        ]));
    }

    public async Task<string> ReasonAndActAsync(string userRequest)
    {
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
        _chatHistory.AddUserMessage(userRequest);

        var result = await chatCompletionService.GetChatMessageContentAsync(_chatHistory, _executionSettings, _kernel);
        var finalResponse = result.Items.LastOrDefault(i => i is TextContent) as TextContent;

        // Add the assistant's response to history for the next turn
        _chatHistory.Add(result);

        // The ToolCallBehavior.AutoInvokeKernelFunctions setting handles the tool execution and adds the result to the history automatically.
        // We just need to return the final text response from the assistant.
        
        Console.WriteLine($"🤖 LLM Response: {finalResponse?.Text ?? "No text response."}");

        return finalResponse?.Text ?? "Task completed.";
    }

    public void PrintConversationLog()
    {
        Console.WriteLine("\n📝 Conversation Log:");
        Console.WriteLine("==================");
        foreach (var entry in _chatHistory)
        {
            Console.WriteLine($"{entry.Role}: {entry.Content}");
        }
        Console.WriteLine("==================\n");
    }
}