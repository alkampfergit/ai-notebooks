namespace Alkampfer.Sgr.Playground.Models;

/// <summary>
/// **Email model** representing sent email communications.
/// This is the central model that binds all other objects together.
/// </summary>
public class Email
{
    /// <summary>
    /// Unique identifier for this email record
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Primary email address - this is the key that binds all other objects
    /// </summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// Sender email address
    /// </summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Email subject line
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Email message body content
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// When the email was sent
    /// </summary>
    public DateTime SentDate { get; set; }

    /// <summary>
    /// Type of email (e.g., "invoice", "notification", "reminder")
    /// </summary>
    public string EmailType { get; set; } = string.Empty;
}
