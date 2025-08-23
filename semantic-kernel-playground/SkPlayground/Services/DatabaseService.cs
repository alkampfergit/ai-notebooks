using System.Text.Json;
using SkPlayground.Models;

namespace SkPlayground.Services;

/// <summary>
/// **Database service** that manages the in-memory database for business operations.
/// 
/// This service provides:
/// - **Database initialization** with default product catalog
/// - **JSON serialization** for system prompts and API responses
/// - **Centralized data management** for all business functions
/// - **Thread-safe access** to shared database collections
/// </summary>
public class DatabaseService
{
    private readonly List<Rule> _rules;
    private readonly Dictionary<string, Invoice> _invoices;
    private readonly List<Email> _emails;
    private readonly Dictionary<string, Product> _products;
    
    /// <summary>
    /// **Rules collection** - provides access to customer-specific business rules.
    /// </summary>
    public List<Rule> Rules => _rules;

    /// <summary>
    /// **Invoices collection** - provides access to generated invoice records.
    /// </summary>
    public Dictionary<string, Invoice> Invoices => _invoices;

    /// <summary>
    /// **Emails collection** - provides access to sent email communications.
    /// </summary>
    public List<Email> Emails => _emails;

    /// <summary>
    /// **Products collection** - provides access to product catalog data.
    /// </summary>
    public Dictionary<string, Product> Products => _products;

    /// <summary>
    /// **Constructor that initializes the database** with default product catalog and empty collections.
    /// 
    /// The database structure matches the Python original with:
    /// - Products: Pre-populated catalog with course offerings
    /// - Rules: Customer-specific business rules (empty initially)
    /// - Invoices: Generated invoice records (empty initially) 
    /// - Emails: Sent email communications (empty initially)
    /// </summary>
    public DatabaseService()
    {
        _rules = new List<Rule>();
        _invoices = new Dictionary<string, Invoice>();
        _emails = new List<Email>();
        _products = new Dictionary<string, Product>
        {
            ["SKU-205"] = new() { Sku = "SKU-205", Name = "AGI 101 Course Personal", Price = 258 },
            ["SKU-210"] = new() { Sku = "SKU-210", Name = "AGI 101 Course Team (5 seats)", Price = 1290 },
            ["SKU-220"] = new() { Sku = "SKU-220", Name = "Building AGI - online exercises", Price = 315 }
        };
    }

    /// <summary>
    /// **Serializes the product catalog** for use in system prompts.
    /// 
    /// This method extracts just the products section of the database
    /// and serializes it to JSON for inclusion in LLM system prompts.
    /// </summary>
    /// <param name="jsonOptions">JSON serialization options</param>
    /// <returns>JSON string representation of the product catalog</returns>
    public string GetProductCatalogAsJson(JsonSerializerOptions jsonOptions)
    {
        return JsonSerializer.Serialize(_products, jsonOptions);
    }

    /// <summary>
    /// **Gets the rules collection** for customer-specific business rules.
    /// </summary>
    /// <returns>List of rule objects</returns>
    public List<Rule> GetRules()
    {
        return _rules;
    }

    /// <summary>
    /// **Gets the invoices collection** for generated invoice records.
    /// </summary>
    /// <returns>Dictionary of invoice records indexed by invoice ID</returns>
    public Dictionary<string, Invoice> GetInvoices()
    {
        return _invoices;
    }

    /// <summary>
    /// **Gets the emails collection** for sent email communications.
    /// </summary>
    /// <returns>List of email objects</returns>
    public List<Email> GetEmails()
    {
        return _emails;
    }

    /// <summary>
    /// **Gets the products collection** for product catalog data.
    /// </summary>
    /// <returns>Dictionary of product records indexed by SKU</returns>
    public Dictionary<string, Product> GetProducts()
    {
        return _products;
    }
}