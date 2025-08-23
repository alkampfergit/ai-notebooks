using System.Text.Json;
using SkPlayground.Models;

namespace SkPlayground.Services;

public class ToolHandler
{
    private readonly Dictionary<string, List<Dictionary<string, object>>> _mockDatabase;
    private readonly List<string> _emailLog;
    private readonly List<Dictionary<string, object>> _invoiceLog;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public ToolHandler(JsonSerializerOptions jsonOptions)
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
            ReportTaskCompletion completion => HandleTaskCompletion(completion.Summary),
            SendEmail email => HandleSendEmail(email.To, email.Subject, email.Body),
            IssueInvoice invoice => HandleIssueInvoice(invoice.Email, invoice.Skus.ToArray(), invoice.DiscountPercent),
            QueryDatabase query => HandleQueryDatabase(query.Query),
            UpdateDatabase update => HandleUpdateDatabase(update.Table, update.Updates, update.WhereClause),
            _ => "Unknown tool type"
        };
    }
    
    public string HandleTaskCompletion(string summary)
    {
        return $"Task completed successfully: {summary}";
    }
    
    public string HandleSendEmail(string to, string subject, string body)
    {
        var logEntry = $"Email sent to {to}: '{subject}'";
        _emailLog.Add(logEntry);
        Console.WriteLine($"📧 {logEntry}");
        return $"Email sent successfully to {to}";
    }
    
    public string HandleIssueInvoice(string email, string[] skus, double discountPercent)
    {
        var products = _mockDatabase["products"];
        var matchedProducts = products.Where(p => skus.Contains(p["sku"]?.ToString())).ToList();
        
        if (!matchedProducts.Any())
        {
            return $"Error: No products found for SKUs: {string.Join(", ", skus)}";
        }
        
        var total = matchedProducts.Sum(p => Convert.ToDouble(p["price"]));
        var discountAmount = total * (discountPercent / 100.0);
        var finalAmount = total - discountAmount;
        
        var invoiceRecord = new Dictionary<string, object>
        {
            ["email"] = email,
            ["skus"] = skus,
            ["total"] = total,
            ["discount_percent"] = discountPercent,
            ["discount_amount"] = discountAmount,
            ["final_amount"] = finalAmount,
            ["timestamp"] = DateTime.UtcNow
        };
        
        _invoiceLog.Add(invoiceRecord);
        
        Console.WriteLine($"💰 Invoice issued to {email}: ${finalAmount:F2} (${total:F2} - ${discountAmount:F2} discount)");
        return $"Invoice issued successfully. Total: ${finalAmount:F2}";
    }
    
    public string HandleQueryDatabase(string query)
    {
        Console.WriteLine($"🔍 Executing query: {query}");
        
        // Simple query simulation
        var results = query.ToLower() switch
        {
            var q when q.Contains("customers") => _mockDatabase["customers"],
            var q when q.Contains("products") => _mockDatabase["products"],
            _ => new List<Dictionary<string, object>>()
        };
        
        return $"Query executed. Found {results.Count} records: {JsonSerializer.Serialize(results, _jsonOptions)}";
    }
    
    public string HandleUpdateDatabase(string table, object updates, string whereClause)
    {
        Console.WriteLine($"✏️ Updating {table} where {whereClause}");
        return $"Database updated successfully. Table: {table}, Updates: {JsonSerializer.Serialize(updates)}";
    }

    internal string GetDbAsJson()
    {
        return JsonSerializer.Serialize(_mockDatabase, _jsonOptions);
    }
}