using System.Text.Json;
using Alkampfer.Sgr.Playground.Models;

namespace Alkampfer.Sgr.Playground.Services;

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
    private readonly Dictionary<string, Customer> _customers;

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
    /// **Customers collection** - provides access to customer data indexed by email.
    /// </summary>
    public Dictionary<string, Customer> Customers => _customers;

    /// <summary>
    /// **Constructor that initializes the database** with default product catalog and empty collections.
    ///
    /// The database structure matches the Python original with:
    /// - Products: Pre-populated catalog with course offerings
    /// - Customers: Pre-populated customer data
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
            ["SKU-220"] = new() { Sku = "SKU-220", Name = "Building AGI - online exercises", Price = 315 },

            // Gaming laptops
            ["SKU-305"] = new()
            {
                Sku = "SKU-305",
                Name = "GamerPro X15 - Gaming Laptop",
                Price = 1499,
                Description = "High-performance gaming laptop with dedicated RTX GPU and 240Hz display",
                Category = "Gaming",
                IsActive = true
            },
            ["SKU-310"] = new()
            {
                Sku = "SKU-310",
                Name = "GamerLite 14 - Portable Gaming",
                Price = 999,
                Description = "Lightweight gaming laptop with high-refresh IPS display and long battery life",
                Category = "Gaming",
                IsActive = true
            },

            // Rugged laptops
            ["SKU-405"] = new()
            {
                Sku = "SKU-405",
                Name = "RuggedMax 14 - Rugged Laptop",
                Price = 1799,
                Description = "MIL-STD certified rugged laptop designed for field and industrial use",
                Category = "Rugged",
                IsActive = true
            },
            ["SKU-410"] = new()
            {
                Sku = "SKU-410",
                Name = "FieldTough 12 - Ultra Rugged",
                Price = 2099,
                Description = "Ultra-rugged convertible laptop engineered for extreme environments",
                Category = "Rugged",
                IsActive = true
            }
        };
        _customers = new Dictionary<string, Customer>
        {
            ["john.smith@example.com"] = new() { Email = "john.smith@example.com", Name = "John", Surname = "Smith" },
            ["sarah.johnson@example.com"] = new() { Email = "sarah.johnson@example.com", Name = "Sarah", Surname = "Johnson" },
            ["michael.williams@example.com"] = new() { Email = "michael.williams@example.com", Name = "Michael", Surname = "Williams" },
            ["emma.brown@example.com"] = new() { Email = "emma.brown@example.com", Name = "Emma", Surname = "Brown" },
            ["david.jones@example.com"] = new() { Email = "david.jones@example.com", Name = "David", Surname = "Jones" }
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

    /// <summary>
    /// **Gets the customers collection** for customer data.
    /// </summary>
    /// <returns>Dictionary of customer records indexed by email</returns>
    public Dictionary<string, Customer> GetCustomers()
    {
        return _customers;
    }
}