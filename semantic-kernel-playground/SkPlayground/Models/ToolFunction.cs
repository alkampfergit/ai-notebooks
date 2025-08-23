using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SkPlayground.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "tool")]
[JsonDerivedType(typeof(ReportTaskCompletion), "report_task_completion")]
[JsonDerivedType(typeof(SendEmail), "send_email")]
[JsonDerivedType(typeof(IssueInvoice), "issue_invoice")]
[JsonDerivedType(typeof(QueryDatabase), "query_database")]
[JsonDerivedType(typeof(UpdateDatabase), "update_database")]
public abstract record ToolFunction
{
    [JsonPropertyName("tool")]
    public abstract string Tool { get; }
}

public record ReportTaskCompletion : ToolFunction
{
    public override string Tool => "report_task_completion";
    
    [JsonPropertyName("summary")]
    [Required]
    public required string Summary { get; init; }
}

public record SendEmail : ToolFunction
{
    public override string Tool => "send_email";
    
    [JsonPropertyName("to")]
    [Required]
    [EmailAddress]
    public required string To { get; init; }
    
    [JsonPropertyName("subject")]
    [Required]
    public required string Subject { get; init; }
    
    [JsonPropertyName("body")]
    [Required]
    public required string Body { get; init; }
}

public record IssueInvoice : ToolFunction
{
    public override string Tool => "issue_invoice";
    
    [JsonPropertyName("email")]
    [Required]
    [EmailAddress]
    public required string Email { get; init; }
    
    [JsonPropertyName("skus")]
    [Required]
    public required List<string> Skus { get; init; }
    
    [JsonPropertyName("discount_percent")]
    [Range(0, 50)]
    public int DiscountPercent { get; init; } = 0;
}

public record QueryDatabase : ToolFunction
{
    public override string Tool => "query_database";
    
    [JsonPropertyName("query")]
    [Required]
    public required string Query { get; init; }
}

public record UpdateDatabase : ToolFunction
{
    public override string Tool => "update_database";
    
    [JsonPropertyName("table")]
    [Required]
    public required string Table { get; init; }
    
    [JsonPropertyName("updates")]
    [Required]
    public required Dictionary<string, object> Updates { get; init; }
    
    [JsonPropertyName("where_clause")]
    [Required]
    public required string WhereClause { get; init; }
}