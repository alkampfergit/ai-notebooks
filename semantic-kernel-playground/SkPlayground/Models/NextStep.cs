using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SkPlayground.BusinessFunctions;

namespace SkPlayground.Models;

[Description("Represents the next step in a task workflow")]
public class NextStep
{
    [Description("Current status of the workflow")]
    public string CurrentState { get; set; }

    [Description("Brief list of next step planned")]
    public List<string> PlanRemainingStepsBrief { get; set; }

    [Description("Indicates if the task is completed")]
    public bool TaskCompleted { get; set; } = false;

    [Description("Tool to call for next action")]
    public ToolCall ToolCall { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SendEmailToolCall), typeDiscriminator: "send_email")]
[JsonDerivedType(typeof(IssueInvoiceToolCall), typeDiscriminator: "issue_invoice")]
[JsonDerivedType(typeof(GetCustomerDataToolCall), typeDiscriminator: "get_customer_data")]
[JsonDerivedType(typeof(VoidInvoiceToolCall), typeDiscriminator: "void_invoice")]
[JsonDerivedType(typeof(CreateRuleToolCall), typeDiscriminator: "create_rule")]
[JsonDerivedType(typeof(ReportTaskCompletionToolCall), typeDiscriminator: "report_task_completion")]
public abstract class ToolCall
{
    public string Tool { get; set; } = default!;
}