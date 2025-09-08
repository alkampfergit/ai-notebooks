using SkPlayground.Models;
using SkPlayground.Services;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// **Business function for customer data retrieval**
/// 
/// This function provides comprehensive customer information by aggregating
/// data from multiple database collections including rules, invoices, and
/// email communications for a complete customer profile.
/// </summary>
public class GetCustomerDataFunction : BusinessFunction<GetCustomerDataToolCall>
{
    private readonly DatabaseService _databaseService;

    public GetCustomerDataFunction(DatabaseService databaseService)
        : base()
    {
        _databaseService = databaseService;
    }

    /// <summary>
    /// **Executes customer data retrieval** by aggregating information across collections.
    /// 
    /// This method provides a comprehensive customer view by:
    /// - Filtering rules associated with the customer email
    /// - Collecting all invoices for the customer
    /// - Gathering email communication history
    /// - Combining data into a unified customer profile
    /// - Logging the data retrieval operation
    /// </summary>
    /// <param name="parameters">Customer data parameters containing the email address</param>
    /// <param name="cancellationToken">Cancellation token to support cooperative cancellation</param>
    /// <returns>BusinessFunctionResult with comprehensive customer data and retrieval summary</returns>
    protected override async Task<BusinessFunctionResult> ExecuteAsync(
        GetCustomerDataToolCall parameters,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rules = _databaseService.GetRules();
        var invoices = _databaseService.GetInvoices();
        var emails = _databaseService.GetEmails();

        // Aggregate customer data from all relevant collections
        if (!parameters.GetAllCustomers)
        {
            // Filter data for specific customer email
            rules = rules.Where(r => r.Email == parameters.Email).ToList();
            invoices = invoices.Where(i => i.Value.Email == parameters.Email).ToDictionary(i => i.Key, i => i.Value);
            emails = emails.Where(e => e.To == parameters.Email).ToList();
        }

        var customerData = new Dictionary<string, object>
        {
            ["rules"] = rules,
            ["invoices"] = invoices,
            ["emails"] = emails
        };

        // Simulate async data retrieval
        await Task.Delay(75, cancellationToken);

        var rulesCount = rules.Count();
        var invoicesCount = invoices.Count();
        var emailsCount = emails.Count();

        var summary = parameters.GetAllCustomers 
            ? $"🔍 Retrieved data for all customers: {rulesCount} rules, {invoicesCount} invoices, {emailsCount} emails"
            : $"🔍 Retrieved customer data for {parameters.Email}: {rulesCount} rules, {invoicesCount} invoices, {emailsCount} emails";
        Console.WriteLine(summary);

        return new BusinessFunctionResult(customerData, summary);
    }
}

/// <summary>
/// **Parameter class for customer data retrieval operations**
/// 
/// Contains the customer identification information required
/// to retrieve comprehensive customer profile data.
/// </summary>
[Description("Retrieves customer data From database using email address")]
public class GetCustomerDataToolCall : ToolCall
{
    [Description("If true, retrieves data for all customers (ignores Email)")]
    public bool GetAllCustomers { get; set; }

    /// <summary>
    /// **Customer email address** - unique identifier used to lookup
    /// and retrieve all associated customer data across the system.
    /// </summary>
    [Description("The customer's email address to look up")]
    [Required]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Type discriminator for polymorphic deserialization
    /// </summary>
    public override string Type => "get_customer_data_tool_call";
}