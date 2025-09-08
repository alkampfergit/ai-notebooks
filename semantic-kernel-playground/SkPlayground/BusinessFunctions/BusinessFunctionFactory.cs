using Fasterflect;
using SkPlayground.Models;
using SkPlayground.Services;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Json.Schema;
using Json.Schema.Generation;

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
/// - Generate JSON schemas for function parameters using System.Text.Json
/// - Maintain consistency between parameter types and function implementations
/// - Store function information in a dictionary for easy access by name
/// - Enable dependency injection and configuration management
/// - Support structured LLM responses with JSON schema validation
/// </summary>
public class BusinessFunctionFactory
{
    private readonly Dictionary<string, FunctionInformations> _functions;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// **Constructor that initializes all business functions with JSON schemas**.
    /// 
    /// Creates all function instances with proper dependencies and generates corresponding
    /// JSON schemas for structured LLM responses.
    /// </summary>
    /// <param name="jsonOptions">JSON serialization options for parameter handling</param>
    /// <param name="databaseService">Database service instance for all functions</param>
    public BusinessFunctionFactory(JsonSerializerOptions jsonOptions, DatabaseService databaseService)
    {
        _jsonOptions = jsonOptions;
        _functions = CreateAllFunctions(jsonOptions, databaseService);
    }

    /// <summary>
    /// **Creates all business function instances** with shared dependencies and JSON schemas.
    /// 
    /// This method instantiates all concrete business functions with:
    /// - Shared JSON serialization options for consistency
    /// - Database service for data operations
    /// - Generated JSON schemas for each parameter type
    /// - Proper dependency injection pattern
    /// </summary>
    /// <param name="jsonOptions">JSON serialization options for parameter handling</param>
    /// <param name="databaseService">Database service instance for all functions</param>
    /// <returns>Dictionary mapping function names to function information with JSON schemas</returns>
    private Dictionary<string, FunctionInformations> CreateAllFunctions(
        JsonSerializerOptions jsonOptions,
        DatabaseService databaseService)
    {
        // Create business function instances
        var reportTaskCompletionFunc = new ReportTaskCompletionFunction(jsonOptions);
        var sendEmailFunc = new SendEmailFunction(jsonOptions, databaseService);
        var issueInvoiceFunc = new IssueInvoiceFunction(jsonOptions, databaseService);
        var getCustomerDataFunc = new GetCustomerDataFunction(jsonOptions, databaseService);
        var voidInvoiceFunc = new VoidInvoiceFunction(jsonOptions, databaseService);
        var createRuleFunc = new CreateRuleFunction(jsonOptions, databaseService);

        // Generate JSON schemas for each parameter type using JsonSchema.Net
        var schemaConfig = new SchemaGeneratorConfiguration
        {
            PropertyNameResolver = PropertyNameResolvers.CamelCase
        };
        
        var reportTaskCompletionSchema = JsonNode.Parse(JsonSerializer.Serialize(new JsonSchemaBuilder().FromType<ReportTaskCompletionToolCall>(schemaConfig).Build()));
        var sendEmailSchema = JsonNode.Parse(JsonSerializer.Serialize(new JsonSchemaBuilder().FromType<SendEmailToolCall>(schemaConfig).Build()));
        var issueInvoiceSchema = JsonNode.Parse(JsonSerializer.Serialize(new JsonSchemaBuilder().FromType<IssueInvoiceToolCall>(schemaConfig).Build()));
        var getCustomerDataSchema = JsonNode.Parse(JsonSerializer.Serialize(new JsonSchemaBuilder().FromType<GetCustomerDataToolCall>(schemaConfig).Build()));
        var voidInvoiceSchema = JsonNode.Parse(JsonSerializer.Serialize(new JsonSchemaBuilder().FromType<VoidInvoiceToolCall>(schemaConfig).Build()));
        var createRuleSchema = JsonNode.Parse(JsonSerializer.Serialize(new JsonSchemaBuilder().FromType<CreateRuleToolCall>(schemaConfig).Build()));

        // Return dictionary with function information including JSON schemas
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
    internal async Task<BusinessFunctionResult> DispatchToolFunction(NextStep nextStep, CancellationToken cancellationToken = default)
    {
        if (nextStep is null) throw new ArgumentNullException(nameof(nextStep));

        // Find the registered function whose parameter type matches the runtime type of nextStep
        var match = _functions.Values.FirstOrDefault(fi =>
            fi.ParameterType != null && fi.ParameterType.IsInstanceOfType(nextStep));

        var businessFunction = match?.BusinessFunction;
        if (businessFunction == null) throw new InvalidOperationException("Business function not available for matched entry.");

        var result = await businessFunction.ExecuteAsync(nextStep, cancellationToken);
        return result;
    }

    /// <summary>
    /// **Generates JSON schema for NextStep type** using JsonSchema.Net.
    /// 
    /// This method creates a JSON schema definition that's compatible with OpenAI's
    /// response_format requirements, including additionalProperties: false recursively.
    /// </summary>
    /// <returns>JSON schema string representing the NextStep structure</returns>
    public string GenerateJsonSchemaForToolCall()
    {
        // Configure JsonSchema.Net generation options for OpenAI compatibility
        var configuration = new SchemaGeneratorConfiguration
        {
            // Use camelCase property naming to match our JSON serialization
            PropertyNameResolver = PropertyNameResolvers.CamelCase,  
        };

        // Generate schema using JsonSchema.Net
        var schema = new JsonSchemaBuilder()
            .FromType<NextStep>(configuration)
            .Build();

        // Convert to JsonNode to recursively add additionalProperties: false
        var schemaJson = JsonSerializer.Serialize(schema);
        var schemaNode = JsonNode.Parse(schemaJson);
        
        // Recursively set additionalProperties to false for all objects
        SetAdditionalPropertiesFalseRecursively(schemaNode);
        
        // Convert back to formatted JSON string
        return schemaNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Helper method to safely check if a JsonNode represents the "object" type.
    /// JSON Schema allows type to be either a string or an array of strings.
    /// </summary>
    private static bool IsObjectType(JsonNode? typeNode)
    {
        if (typeNode is JsonValue jsonValue)
        {
            // Single type as string
            try
            {
                return jsonValue.GetValue<string>() == "object";
            }
            catch
            {
                return false;
            }
        }
        else if (typeNode is JsonArray jsonArray)
        {
            // Array of types
            return jsonArray.Any(item => 
                item is JsonValue value && 
                value.TryGetValue<string>(out var str) && 
                str == "object");
        }
        
        return false;
    }

    /// <summary>
    /// Recursively sets additionalProperties to false and makes all properties required for all object types in the schema.
    /// This ensures OpenAI compatibility by preventing additional properties and requiring all defined properties.
    /// </summary>
    private static void SetAdditionalPropertiesFalseRecursively(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            // If this object has type "object", set additionalProperties to false
            // Handle both string type and array of types (JSON Schema supports both)
            if (obj.ContainsKey("type") && IsObjectType(obj["type"]))
            {
                obj["additionalProperties"] = false;

                // For OpenAI compatibility with additionalProperties: false,
                // ALL properties must be in the required array, not just those with [Required] attributes
                if (obj.ContainsKey("properties") && obj["properties"] is JsonObject properties)
                {
                    var requiredArray = new JsonArray();
                    foreach (var property in properties)
                    {
                        requiredArray.Add(property.Key);
                    }
                    
                    if (requiredArray.Count > 0)
                    {
                        obj["required"] = requiredArray;
                    }
                }
            }

            // Recursively process all child nodes
            foreach (var kvp in obj.ToArray())
            {
                SetAdditionalPropertiesFalseRecursively(kvp.Value);
            }
        }
        else if (node is JsonArray array)
        {
            // Recursively process array elements
            foreach (var item in array)
            {
                SetAdditionalPropertiesFalseRecursively(item);
            }
        }
    }
}