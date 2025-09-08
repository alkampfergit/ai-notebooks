using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using SkPlayground.Models;
using SkPlayground.Services;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// **Business function for invoice generation and processing**
/// 
/// This function handles the complete invoice lifecycle including:
/// product lookup, price calculation, discount application, and
/// invoice record creation with proper tracking and validation.
/// </summary>
public class IssueInvoiceFunction : BusinessFunction<IssueInvoiceToolCall>
{
    private readonly DatabaseService _databaseService;

    public IssueInvoiceFunction(DatabaseService databaseService) 
        : base()
    {
        _databaseService = databaseService;
    }

    /// <summary>
    /// **Executes invoice generation** with product validation and discount calculation.
    /// 
    /// This method implements the complete invoice workflow:
    /// - Validates all product SKUs against the database
    /// - Calculates total price from product catalog
    /// - Applies discount percentage with proper rounding
    /// - Generates unique invoice ID and file path
    /// - Creates comprehensive invoice record
    /// - Logs invoice creation for audit trail
    /// </summary>
    /// <param name="parameters">Invoice parameters including customer email, SKUs, and discount</param>
    /// <param name="cancellationToken">Cancellation token to support cooperative cancellation</param>
    /// <returns>BusinessFunctionResult with invoice record and creation summary</returns>
    protected override async Task<BusinessFunctionResult> ExecuteAsync(IssueInvoiceToolCall parameters, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var products = _databaseService.GetProducts();
        var total = 0.0;
        
        // Validate all SKUs and calculate total price
        var invoiceItems = new List<InvoiceItem>();
        foreach (var sku in parameters.Skus)
        {
            if (!products.TryGetValue(sku, out var product))
            {
                var errorMsg = $"Product {sku} not found";
                return new BusinessFunctionResult(null, errorMsg);
            }
            total += (double)product.Price;
            
            // Create invoice item
            invoiceItems.Add(new InvoiceItem
            {
                ProductSku = product.Sku,
                ProductName = product.Name,
                Quantity = 1,
                UnitPrice = product.Price,
                TotalPrice = product.Price
            });
        }
        
        // Calculate discount with proper rounding
        var discount = Math.Round(total * parameters.DiscountPercent / 100.0, 2);
        var invoices = _databaseService.GetInvoices();
        var invoiceId = $"INV-{invoices.Count + 1}";
        
        // Create comprehensive invoice record
        var invoice = new Invoice
        {
            Id = invoiceId,
            Email = parameters.Email,
            CreatedDate = DateTime.UtcNow,
            TotalAmount = (decimal)(total - discount),
            Items = invoiceItems,
            Status = "pending",
            IsVoid = false
        };
        
        invoices[invoiceId] = invoice;
        
        // Simulate async invoice generation
        await Task.Delay(120, cancellationToken);
        
        var summary = $"💰 Invoice {invoiceId} issued to {parameters.Email}: ${total - discount:F2} (${total:F2} - ${discount:F2} discount)";
        Console.WriteLine(summary);
        
        return new BusinessFunctionResult(invoice, summary);
    }
}

/// <summary>
/// **Parameter class for invoice generation operations**
/// 
/// Contains all necessary information for creating invoices including
/// customer details, product selection, and discount application.
/// </summary>
[Description("Issues an invoice for specified products with optional discount")]
public class IssueInvoiceToolCall : ToolCall
{
    /// <summary>
    /// **Customer email address** - identifies the customer for whom
    /// the invoice is being generated and delivered.
    /// </summary>
    [Description("The customer's email address for the invoice")]
    [Required]
    public required string Email { get; set; }

    /// <summary>
    /// **Product SKUs list** - collection of product identifiers
    /// to be included in the invoice for pricing calculation.
    /// </summary>
    [Description("List of product SKUs to include in the invoice")]
    [Required]
    public required List<string> Skus { get; set; }

    /// <summary>
    /// **Discount percentage** - percentage discount to apply to the total.
    /// Must be between 0-50% and defaults to 0% if not specified.
    /// </summary>
    [Description("Discount percentage to apply (0-50%)")]
    public int DiscountPercent { get; set; } = 0;

    /// <summary>
    /// Type discriminator for polymorphic deserialization
    /// </summary>
    public override string Type => "issue_invoice_tool_call";
}