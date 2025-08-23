using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkPlayground.Models;
using SkPlayground.Services;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// **Business function for email sending operations**
/// 
/// This function handles email composition and delivery with support for
/// file attachments. It manages the complete email workflow from parameter
/// validation to delivery simulation and logging.
/// </summary>
public class SendEmailFunction : BusinessFunction<SendEmailParameters>
{
    private readonly DatabaseService _databaseService;

    public SendEmailFunction(JsonSerializerOptions jsonOptions, DatabaseService databaseService) 
        : base(jsonOptions)
    {
        _databaseService = databaseService;
    }

    /// <summary>
    /// **Executes email sending operation** by composing and delivering an email.
    /// 
    /// This method handles the complete email workflow:
    /// - Creates email record with recipient, subject, and message
    /// - Logs email to the database for tracking
    /// - Simulates email delivery with console output
    /// - Returns the created email record for confirmation
    /// </summary>
    /// <param name="parameters">Email parameters including recipient, subject, message, and attachments</param>
    /// <param name="cancellationToken">Cancellation token to support cooperative cancellation</param>
    /// <returns>BusinessFunctionResult with email record and delivery summary</returns>
    protected override async Task<BusinessFunctionResult> ExecuteAsync(SendEmailParameters parameters, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var email = new Email
        {
            To = parameters.RecipientEmail,
            Subject = parameters.Subject,
            Message = parameters.Message
        };
        
        var emails = _databaseService.GetEmails();
        emails.Add(email);
        
        // Simulate async email sending
        await Task.Delay(100, cancellationToken);
        
        var summary = $"📧 Email sent to {parameters.RecipientEmail}: '{parameters.Subject}'";
        Console.WriteLine(summary);
        
        return new BusinessFunctionResult(email, summary);
    }
}

/// <summary>
/// **Parameter class for email sending operations**
/// 
/// Defines all required and optional parameters for composing
/// and sending emails with attachment support.
/// </summary>
[Description("Sends an email with optional file attachments")]
public class SendEmailParameters : NextStep
{
    /// <summary>
    /// **Email subject line** - the title/topic of the email message
    /// that appears in the recipient's inbox.
    /// </summary>
    [Description("The subject line of the email")]
    public string Subject { get; set; }

    /// <summary>
    /// **Email body content** - the main message text that will be
    /// delivered to the recipient.
    /// </summary>
    [Description("The body content of the email")]
    public string Message { get; set; }

    /// <summary>
    /// **File attachments** - list of file paths to be attached to the email.
    /// Defaults to empty list if no attachments are needed.
    /// </summary>
    [Description("List of file paths to attach to the email")]
    public List<string> Files { get; set; } = [];

    /// <summary>
    /// **Recipient email address** - the destination email address
    /// where the message will be delivered.
    /// </summary>
    [Description("The email address of the recipient")]
    public string RecipientEmail { get; set; }
}