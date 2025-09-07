using System.ComponentModel;
using System.Text.Json;
using SkPlayground.Models;
using SkPlayground.Services;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// **Business function for customer rule creation and management**
/// 
/// This function handles the creation of custom business rules for specific
/// customers, enabling personalized business logic and automated decision-making
/// based on customer-specific requirements and preferences.
/// </summary>
public class CreateRuleFunction : BusinessFunction<CreateRuleToolCall>
{
    private readonly DatabaseService _databaseService;

    public CreateRuleFunction(JsonSerializerOptions jsonOptions, DatabaseService databaseService) 
        : base(jsonOptions)
    {
        _databaseService = databaseService;
    }

    /// <summary>
    /// **Executes rule creation** by storing customer-specific business rules.
    /// 
    /// This method handles the complete rule creation workflow:
    /// - Creates a new rule record with customer association
    /// - Associates the rule with the specified customer email
    /// - Stores the rule in the database for future reference
    /// - Logs the rule creation for audit and tracking purposes
    /// - Returns the created rule record for confirmation
    /// </summary>
    /// <param name="parameters">Rule creation parameters including customer email and rule definition</param>
    /// <param name="cancellationToken">Cancellation token to support cooperative cancellation</param>
    /// <returns>Created rule record containing customer email and rule details</returns>
    protected override async Task<BusinessFunctionResult> ExecuteAsync(CreateRuleToolCall parameters, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // Create rule record with customer association
        var rule = new Rule
        {
            Id = Guid.NewGuid().ToString(),
            Email = parameters.Email,
            Description = parameters.Rule,
            RuleType = "custom",
            IsActive = true
        };
        
        var rules = _databaseService.GetRules();
        rules.Add(rule);
        
        // Simulate async rule creation
        await Task.Delay(50, cancellationToken);
        
        var summary = $"📝 Rule created for {parameters.Email}: {parameters.Rule}";
        Console.WriteLine(summary);
        
        return new BusinessFunctionResult(rule, summary);
    }
}

/// <summary>
/// **Parameter class for customer rule creation operations**
/// 
/// Contains the customer identification and rule definition
/// required for creating customer-specific business rules.
/// </summary>
[Description("Creates a rule for a specific customer")]
public class CreateRuleToolCall : ToolCall
{
    /// <summary>
    /// **Customer email address** - identifies the customer for whom
    /// the custom business rule is being created and applied.
    /// </summary>
    [Description("The customer's email address for the rule")]
    public string Email { get; set; }

    /// <summary>
    /// **Business rule definition** - the specific rule logic or condition
    /// that will be created and stored for this customer.
    /// </summary>
    [Description("The rule to be created and stored")]
    public string Rule { get; set; }

    /// <summary>
    /// Type discriminator for polymorphic deserialization
    /// </summary>
    public override string Type => "create_rule_tool_call";
}