using SkPlayground.Models;
using SkPlayground.Services;
using SkPlayground.Utils;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// **Record containing business function and parameter type information**
/// 
/// This record pairs together:
/// - **BusinessFunction**: The concrete business logic implementation
/// - **ParameterType**: The CLR Type used for deserializing the function argument
/// - **JsonSchema**: The JSON schema for the function parameter type
/// 
/// This enables direct business function calls with JSON schema validation
/// for structured LLM responses.
/// </summary>
/// <param name="BusinessFunction">The concrete business function instance</param>
/// <param name="ParameterType">The CLR Type of the function parameter (e.g. FooToolCall)</param>
/// <param name="JsonSchema">The JSON schema for the parameter type</param>
public class FunctionInformations
{
    public BusinessFunction BusinessFunction { get; set; }
    public Type ParameterType { get; set; }
    public JsonNode JsonSchema { get; set; }

    public FunctionInformations(BusinessFunction businessFunction, Type parameterType, JsonNode jsonSchema)
    {
        BusinessFunction = businessFunction;
        ParameterType = parameterType;
        JsonSchema = jsonSchema;
    }
}

/// <summary>
/// **Factory for creating and managing business function instances with JSON schemas**
/// 
/// This factory provides a centralized way to:
/// - Create instances of all business functions with proper dependencies
/// - Generate JSON schemas for function parameters using PolymorphicSchemaManager
/// - Maintain consistency between parameter types and function implementations
/// - Store function information in a dictionary for easy access by name
/// - Enable dependency injection and configuration management
/// - Support structured LLM responses with JSON schema validation and polymorphic deserialization
/// </summary>
public class BusinessFunctionFactory
{
    private readonly Dictionary<string, FunctionInformations> _functions;

    private readonly PolymorphicSchemaManager<NextStep, ToolCall> _schemaManager;

    /// <summary>
    /// **Constructor that initializes all business functions with polymorphic schema support**.
    /// 
    /// Creates all function instances with proper dependencies and configures the PolymorphicSchemaManager
    /// for NextStep/ToolCall polymorphic handling and JSON schema generation.
    /// </summary>
    /// <param name="jsonOptions">JSON serialization options for parameter handling</param>
    /// <param name="databaseService">Database service instance for all functions</param>
    public BusinessFunctionFactory(DatabaseService databaseService)
    {
        // **Initialize PolymorphicSchemaManager with all ToolCall derived types**
        _schemaManager = new PolymorphicSchemaManager<NextStep, ToolCall>("type")
            .AddDerivedTypes(
                typeof(ReportTaskCompletionToolCall),
                typeof(SendEmailToolCall), 
                typeof(IssueInvoiceToolCall),
                typeof(GetCustomerDataToolCall),
                typeof(VoidInvoiceToolCall),
                typeof(CreateRuleToolCall)
            );
            
        _functions = CreateAllFunctions(databaseService);
    }

    /// <summary>
    /// **Creates all business function instances** with shared dependencies and JSON schemas.
    /// 
    /// This method instantiates all concrete business functions with:
    /// - Shared JSON serialization options for consistency
    /// - Database service for data operations
    /// - JSON schemas managed by PolymorphicSchemaManager
    /// - Proper dependency injection pattern
    /// </summary>
    /// <param name="jsonOptions">JSON serialization options for parameter handling</param>
    /// <param name="databaseService">Database service instance for all functions</param>
    /// <returns>Dictionary mapping function names to function information with JSON schemas</returns>
    private Dictionary<string, FunctionInformations> CreateAllFunctions(
        DatabaseService databaseService)
    {
        // **Create business function instances**
        var reportTaskCompletionFunc = new ReportTaskCompletionFunction();
        var sendEmailFunc = new SendEmailFunction(databaseService);
        var issueInvoiceFunc = new IssueInvoiceFunction(databaseService);
        var getCustomerDataFunc = new GetCustomerDataFunction(databaseService);
        var voidInvoiceFunc = new VoidInvoiceFunction(databaseService);
        var createRuleFunc = new CreateRuleFunction(databaseService);

        // **Generate individual schemas for each ToolCall type using PolymorphicSchemaManager**
        // This allows getting schema for specific tool call types when needed
        var reportTaskCompletionSchema = JsonNode.Parse(_schemaManager.GenerateSchema(new[] { typeof(ReportTaskCompletionToolCall) }));
        var sendEmailSchema = JsonNode.Parse(_schemaManager.GenerateSchema(new[] { typeof(SendEmailToolCall) }));
        var issueInvoiceSchema = JsonNode.Parse(_schemaManager.GenerateSchema(new[] { typeof(IssueInvoiceToolCall) }));
        var getCustomerDataSchema = JsonNode.Parse(_schemaManager.GenerateSchema(new[] { typeof(GetCustomerDataToolCall) }));
        var voidInvoiceSchema = JsonNode.Parse(_schemaManager.GenerateSchema(new[] { typeof(VoidInvoiceToolCall) }));
        var createRuleSchema = JsonNode.Parse(_schemaManager.GenerateSchema(new[] { typeof(CreateRuleToolCall) }));

        // **Return dictionary with function information including JSON schemas**
        return new Dictionary<string, FunctionInformations>
        {
            ["reportTaskCompletion"] = new FunctionInformations(reportTaskCompletionFunc, typeof(ReportTaskCompletionToolCall), reportTaskCompletionSchema),
            ["sendEmail"] = new FunctionInformations(sendEmailFunc, typeof(SendEmailToolCall), sendEmailSchema),
            ["issueInvoice"] = new FunctionInformations(issueInvoiceFunc, typeof(IssueInvoiceToolCall), issueInvoiceSchema),
            ["getCustomerData"] = new FunctionInformations(getCustomerDataFunc, typeof(GetCustomerDataToolCall), getCustomerDataSchema),
            ["voidInvoice"] = new FunctionInformations(voidInvoiceFunc, typeof(VoidInvoiceToolCall), voidInvoiceSchema),
            ["createRule"] = new FunctionInformations(createRuleFunc, typeof(CreateRuleToolCall), createRuleSchema)
        };
    }

    /// <summary>
    /// Dispatch the provided NextStep to the matching BusinessFunction.
    /// The method finds the registered function whose ParameterType matches the concrete type of nextStep,
    /// invokes an Execute/ExecuteAsync method on the BusinessFunction (with or without a CancellationToken),
    /// awaits Task results when necessary and returns the resulting object (or null).
    /// </summary>
    internal async Task<BusinessFunctionResult> DispatchToolFunction(
        ToolCall toolCall, 
        CancellationToken cancellationToken = default)
    {
        if (toolCall is null) throw new ArgumentNullException(nameof(toolCall));

        // Find the registered function whose parameter type matches the runtime type of nextStep
        var match = _functions.Values.FirstOrDefault(fi =>
            fi.ParameterType != null && fi.ParameterType.IsInstanceOfType(toolCall));

        var businessFunction = match?.BusinessFunction;
        if (businessFunction == null) throw new InvalidOperationException("Business function not available for matched entry.");

        var result = await businessFunction.ExecuteAsync(toolCall, cancellationToken);
        return result;
    }

    /// <summary>
    /// **Generates JSON schema for NextStep type** using PolymorphicSchemaManager.
    /// 
    /// This method creates an OpenAI-compatible JSON schema definition that includes
    /// all configured ToolCall types with proper polymorphic support and discriminators.
    /// The schema is automatically configured with additionalProperties: false and proper
    /// const/enum discriminators for each ToolCall type.
    /// </summary>
    /// <returns>JSON schema string representing the NextStep structure with polymorphic ToolCall support</returns>
    public string GenerateJsonSchemaForToolCall()
    {
        return _schemaManager.GenerateSchema();
    }

    /// <summary>
    /// **Deserializes JSON into a NextStep object with polymorphic ToolCall support**.
    /// 
    /// This method uses the PolymorphicSchemaManager to properly deserialize NextStep objects
    /// where the ToolCall property can be any of the configured derived types. The correct
    /// concrete ToolCall type is determined by the "type" discriminator property.
    /// </summary>
    /// <param name="json">JSON string to deserialize</param>
    /// <returns>NextStep object with correctly typed ToolCall property</returns>
    public NextStep? DeserializeNextStep(string json)
    {
        // **Use PolymorphicSchemaManager for proper polymorphic deserialization**
        // This automatically handles discriminator-based type resolution for ToolCall property
        return _schemaManager.DeserializeFromJson(json);
    }

    /// <summary>
    /// **Provides access to the PolymorphicSchemaManager instance** for advanced schema operations.
    /// 
    /// This allows external components to:
    /// - Generate schemas with specific ToolCall type subsets
    /// - Access polymorphic deserialization capabilities
    /// - Perform schema validation and type checking
    /// </summary>
    public PolymorphicSchemaManager<NextStep, ToolCall> SchemaManager => _schemaManager;
}