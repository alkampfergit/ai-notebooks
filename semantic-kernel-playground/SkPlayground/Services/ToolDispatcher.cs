using System.Text.Json;
using SkPlayground.Models;

namespace SkPlayground.Services;

public class ToolDispatcher
{
    private readonly Dictionary<string, List<Dictionary<string, object>>> _mockDatabase;
    private readonly List<string> _emailLog;
    private readonly List<Dictionary<string, object>> _invoiceLog;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public ToolDispatcher(JsonSerializerOptions jsonOptions)
    {
        _jsonOptions = jsonOptions;
        _mockDatabase = new Dictionary<string, List<Dictionary<string, object>>>
        {
            ["customers"] = new()
            {
                new() { ["id"] = 1, ["email"] = "john@example.com", ["name"] = "John Doe" },
                new() { ["id"] = 2, ["email"] = "jane@example.com", ["name"] = "Jane Smith" }
            },
            ["products"] = new()
            {
                new() { ["sku"] = "LAPTOP001", ["name"] = "Gaming Laptop", ["price"] = 1200.00 },
                new() { ["sku"] = "MOUSE001", ["name"] = "Wireless Mouse", ["price"] = 25.99 }
            }
        };
        
        _emailLog = new List<string>();
        _invoiceLog = new List<Dictionary<string, object>>();
    }
    
    public async Task<string> DispatchAsync(ToolFunction tool)
    {
        await Task.Delay(100); // Simulate async operation
        
        return tool switch
        {
            ReportTaskCompletion completion => HandleTaskCompletion(completion),
            SendEmail email => HandleSendEmail(email),
            IssueInvoice invoice => HandleIssueInvoice(invoice),
            QueryDatabase query => HandleQueryDatabase(query),
            UpdateDatabase update => HandleUpdateDatabase(update),
            _ => "Unknown tool type"
        };
    }
    
    private string HandleTaskCompletion(ReportTaskCompletion completion)
    {
        return $"Task completed successfully: {completion.Summary}";
    }
    
    private string HandleSendEmail(SendEmail email)
    {
        var logEntry = $"Email sent to {email.To}: '{email.Subject}'";
        _emailLog.Add(logEntry);
        Console.WriteLine($"📧 {logEntry}");
        return $"Email sent successfully to {email.To}";
    }
    
    private string HandleIssueInvoice(IssueInvoice invoice)
    {
        var products = _mockDatabase["products"];
        var matchedProducts = products.Where(p => invoice.Skus.Contains(p["sku"]?.ToString())).ToList();
        
        if (!matchedProducts.Any())
        {
            return $"Error: No products found for SKUs: {string.Join(", ", invoice.Skus)}";
        }
        
        var total = matchedProducts.Sum(p => Convert.ToDouble(p["price"]));
        var discountAmount = total * (invoice.DiscountPercent / 100.0);
        var finalAmount = total - discountAmount;
        
        var invoiceRecord = new Dictionary<string, object>
        {
            ["email"] = invoice.Email,
            ["skus"] = invoice.Skus,
            ["total"] = total,
            ["discount_percent"] = invoice.DiscountPercent,
            ["discount_amount"] = discountAmount,
            ["final_amount"] = finalAmount,
            ["timestamp"] = DateTime.UtcNow
        };
        
        _invoiceLog.Add(invoiceRecord);
        
        Console.WriteLine($"💰 Invoice issued to {invoice.Email}: ${finalAmount:F2} (${total:F2} - ${discountAmount:F2} discount)");
        return $"Invoice issued successfully. Total: ${finalAmount:F2}";
    }
    
    private string HandleQueryDatabase(QueryDatabase query)
    {
        Console.WriteLine($"🔍 Executing query: {query.Query}");
        
        // Simple query simulation
        var results = query.Query.ToLower() switch
        {
            var q when q.Contains("customers") => _mockDatabase["customers"],
            var q when q.Contains("products") => _mockDatabase["products"],
            _ => new List<Dictionary<string, object>>()
        };
        
        return $"Query executed. Found {results.Count} records: {JsonSerializer.Serialize(results, _jsonOptions)}";
    }
    
    private string HandleUpdateDatabase(UpdateDatabase update)
    {
        Console.WriteLine($"✏️ Updating {update.Table} where {update.WhereClause}");
        return $"Database updated successfully. Table: {update.Table}, Updates: {JsonSerializer.Serialize(update.Updates)}";
    }
}