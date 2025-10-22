using SkPlayground.Models;
using SkPlayground.Services;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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
        var customers = _databaseService.GetCustomers();

        // First check if the customer exists in the catalog
        if (!customers.TryGetValue(parameters.Email, out var customer))
        {
            const string noCustomerSummaryTemplate = "No customer found with email {0}.";
            var nocustomerSummary = string.Format(noCustomerSummaryTemplate, parameters.Email);

            var missingCustomerData = new Dictionary<string, object?>
            {
                ["customer"] = null,
                ["rules"] = new List<Rule>(),
                ["invoices"] = new Dictionary<string, Invoice>(),
                ["emails"] = new List<Email>(),
                ["summary"] = nocustomerSummary
            };

            await Task.Delay(50, cancellationToken);
            return new BusinessFunctionResult(missingCustomerData, nocustomerSummary);
        }

        // Filter data for existing customer
        rules = rules.Where(r => r.Email == parameters.Email).ToList();
        invoices = invoices.Where(i => i.Value.Email == parameters.Email).ToDictionary(i => i.Key, i => i.Value);
        emails = emails.Where(e => e.To == parameters.Email).ToList();

        var rulesCount = rules.Count;
        var invoicesCount = invoices.Count;
        var emailsCount = emails.Count;

        var summary = $"Found customer {customer.Name} {customer.Surname} ({customer.Email}): {rulesCount} rules, {invoicesCount} invoices, {emailsCount} emails.";

        var customerData = new Dictionary<string, object>
        {
            ["customer"] = customer,
            ["rules"] = rules,
            ["invoices"] = invoices,
            ["emails"] = emails,
            ["summary"] = summary
        };

        // Simulate async data retrieval
        await Task.Delay(75, cancellationToken);

        return new BusinessFunctionResult(customerData, summary);
    }
}

/// <summary>
/// **Parameter class for customer data retrieval operations**
///
/// Contains the customer identification information required
/// to retrieve comprehensive customer profile data.
/// </summary>
[Description("Retrieves customer data from database using email address")]
public class GetCustomerDataToolCall : ToolCall
{
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
    public override string Type => "GetCustomerData";
}
