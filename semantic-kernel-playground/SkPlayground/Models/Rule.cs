namespace SkPlayground.Models;

/// <summary>
/// **Rule model** representing customer-specific business rules.
/// Bound to customers via email address rather than customer ID.
/// </summary>
public class Rule
{
    /// <summary>
    /// Unique identifier for this rule
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer email address - primary key for linking to customer
    /// </summary>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of business rule (e.g., "pricing", "approval", "notification")
    /// </summary>
    public string RuleType { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable description of what this rule does
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Configuration parameters for this rule
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    /// <summary>
    /// Whether this rule is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;
}
