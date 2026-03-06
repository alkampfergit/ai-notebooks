namespace Alkampfer.Sgr.Playground.Models;

/// <summary>
/// **Invoice model** representing generated invoice records.
/// Bound to customers via email address rather than customer ID.
/// </summary>
public class Invoice
{
    /// <summary>
    /// Unique identifier for this invoice
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Customer email address - primary key for linking to customer
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// When the invoice was created
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Total amount of the invoice
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// List of items included in this invoice
    /// </summary>
    public List<InvoiceItem> Items { get; set; } = new();

    /// <summary>
    /// Current status of the invoice (e.g., "pending", "sent", "paid", "void")
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Whether this invoice has been voided
    /// </summary>
    public bool IsVoid { get; set; } = false;

    /// <summary>
    /// Reason for voiding (if applicable)
    /// </summary>
    public string VoidReason { get; set; } = string.Empty;

    /// <summary>
    /// Void this invoice with a reason
    /// </summary>
    public void Void(string reason)
    {
        IsVoid = true;
        VoidReason = reason;
        Status = "void";
    }
}

/// <summary>
/// **Invoice item model** representing individual line items in an invoice.
/// </summary>
public class InvoiceItem
{
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}
