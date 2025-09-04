using System.ComponentModel;
using System.Text.Json;
using SkPlayground.Models;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// **Business function for task completion reporting** 
/// 
/// This function concludes a workflow process with a summary, providing
/// a final status report for completed tasks. It combines both the
/// parameter definition and execution logic in a single cohesive unit.
/// </summary>
public class ReportTaskCompletionFunction : BusinessFunction<ReportTaskCompletionToolCall>
{
    public ReportTaskCompletionFunction(JsonSerializerOptions jsonOptions) : base(jsonOptions)
    {
    }

    /// <summary>
    /// **Executes task completion reporting** by generating a completion summary.
    /// 
    /// This method provides closure to a workflow by:
    /// - Processing the completion summary
    /// - Returning a formatted completion message
    /// - Ensuring proper task lifecycle management
    /// </summary>
    /// <param name="parameters">Task completion parameters including summary</param>
    /// <param name="cancellationToken">Cancellation token to support cooperative cancellation</param>
    /// <returns>Formatted completion message</returns>
    protected override async Task<BusinessFunctionResult> ExecuteAsync(ReportTaskCompletionToolCall parameters, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // Simulate async reporting operation
        await Task.Delay(30, cancellationToken);
        
        var summary = $"Task completed successfully: {parameters.Summary}";
        return new BusinessFunctionResult(null, summary);
    }
}

/// <summary>
/// **Parameter class for task completion reporting**
/// 
/// Contains all the necessary information required to report
/// task completion with appropriate summary details.
/// </summary>
[Description("Conclude the process with a summary")]
public class ReportTaskCompletionToolCall : ToolCall
{
    /// <summary>
    /// **Summary of the completed task** - provides a comprehensive
    /// overview of what was accomplished during the workflow execution.
    /// </summary>
    [Description("A summary of the completed task")]
    public string Summary { get; set; }
}