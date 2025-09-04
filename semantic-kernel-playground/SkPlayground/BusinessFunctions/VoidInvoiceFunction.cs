using System.ComponentModel;
using System.Text.Json;
using SkPlayground.Models;
using SkPlayground.Services;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// **Business function for invoice cancellation (voiding)**
/// 
/// This function handles the cancellation of existing invoices by marking
/// them as void with proper reason tracking for audit and compliance purposes.
/// </summary>
public class VoidInvoiceFunction : BusinessFunction<VoidInvoiceToolCall>
{
    private readonly DatabaseService _databaseService;

    public VoidInvoiceFunction(JsonSerializerOptions jsonOptions, DatabaseService databaseService) 
        : base(jsonOptions)
    {
        _databaseService = databaseService;
    }

    /// <summary>
    /// **Executes invoice voiding operation** with validation and audit tracking.
    /// 
    /// This method handles the complete voiding workflow:
    /// - Validates that the invoice exists in the system
    /// - Marks the invoice as void with timestamp
    /// - Records the reason for voiding for audit purposes
    /// - Logs the voiding operation for compliance tracking
    /// - Returns error message if invoice is not found
    /// </summary>
    /// <param name="parameters">Voiding parameters including invoice ID and reason</param>
    /// <param name="cancellationToken">Cancellation token to support cooperative cancellation</param>
    /// <returns>BusinessFunctionResult with updated invoice record and voiding summary</returns>
    protected override async Task<BusinessFunctionResult> ExecuteAsync(VoidInvoiceToolCall parameters, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var invoices = _databaseService.GetInvoices();
        
        // Validate invoice existence before processing
        if (!invoices.TryGetValue(parameters.InvoiceId, out var invoice))
        {
            var errorMsg = $"Invoice {parameters.InvoiceId} not found";
            return new BusinessFunctionResult(null, errorMsg);
        }
        
        // Mark invoice as void with reason for audit trail
        invoice.Void(parameters.Reason);
        
        // Simulate async voiding operation
        await Task.Delay(60, cancellationToken);
        
        var summary = $"❌ Invoice {parameters.InvoiceId} voided: {parameters.Reason}";
        Console.WriteLine(summary);
        
        return new BusinessFunctionResult(invoice, summary);
    }
}

/// <summary>
/// **Parameter class for invoice voiding operations**
/// 
/// Contains the invoice identification and cancellation reason
/// required for proper invoice voiding with audit compliance.
/// </summary>
[Description("Voids an existing invoice with a reason")]
public class VoidInvoiceToolCall : ToolCall
{
    /// <summary>
    /// **Invoice unique identifier** - the specific invoice ID
    /// that needs to be cancelled/voided in the system.
    /// </summary>
    [Description("The unique identifier of the invoice to void")]
    public string InvoiceId { get; set; }

    /// <summary>
    /// **Voiding reason** - explanation for why the invoice is being
    /// cancelled, required for audit trail and compliance purposes.
    /// </summary>
    [Description("The reason for voiding the invoice")]
    public string Reason { get; set; }
}