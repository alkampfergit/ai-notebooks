namespace SkPlayground.Models;

/// <summary>
/// **Customer data model** representing a customer record in the database.
/// </summary>
public class Customer
{
    /// <summary>
    /// Customer email address (unique identifier)
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Customer first name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Customer last name/surname
    /// </summary>
    public string Surname { get; set; } = string.Empty;
}
