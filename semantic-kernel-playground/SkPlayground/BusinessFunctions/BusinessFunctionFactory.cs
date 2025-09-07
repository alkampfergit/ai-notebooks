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
    /// **Dictionary containing all registered functions** indexed by function name.
    /// Each entry contains the business function instance, parameter type, and JSON schema.
    /// </summary>
    public IReadOnlyDictionary<string, FunctionInformations> Functions => _functions;

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
    /// **Gets all JSON schemas** for LLM integration with structured responses.
    /// 
    /// This method extracts all JSON schema instances from the function information
    /// and returns them as a dictionary for use with structured LLM responses.
    /// </summary>
    /// <returns>Dictionary mapping function names to JSON schema nodes</returns>
    public Dictionary<string, JsonNode> GetAllJsonSchemas()
    {
        return _functions.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.JsonSchema);
    }

    /// <summary>
    /// **Gets all business functions** for direct programmatic access.
    /// 
    /// This method extracts all BusinessFunction instances from the function pairs
    /// and returns them as a dictionary for direct business logic execution.
    /// </summary>
    /// <returns>Dictionary mapping function names to business function instances</returns>
    public Dictionary<string, BusinessFunction> GetAllBusinessFunctions()
    {
        return _functions.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.BusinessFunction);
    }

    /// <summary>
    /// **Gets a specific function information** by name.
    /// 
    /// This method allows access to the business function, parameter type, and JSON schema
    /// for a specific function by its registered name.
    /// </summary>
    /// <param name="functionName">The name of the function to retrieve</param>
    /// <returns>FunctionInformations containing business function, parameter type, and JSON schema, or null if not found</returns>
    public FunctionInformations? GetFunction(string functionName)
    {
        return _functions.TryGetValue(functionName, out var info) ? info : null;
    }

    /// <summary>
    /// Deserialize the given JSON argument string to the correct parameter type for the named function.
    /// Returns the deserialized object cast to NextStep (or null if not found/deserialization fails).
    /// </summary>
    public NextStep? DeserializeArgument(string functionName, string json)
    {
        if (!_functions.TryGetValue(functionName, out var pair) || pair.ParameterType == null)
        {
            return null;
        }

        try
        {
            var obj = JsonSerializer.Deserialize(json, pair.ParameterType, _jsonOptions);
            return obj as NextStep;
        }
        catch (JsonException)
        {
            // Failed to deserialize
            return null;
        }
    }

    /// <summary>
    /// **Creates a parameter object from function call arguments**.
    /// 
    /// This method handles two scenarios:
    /// 1. If "nextStep" argument exists, deserialize it directly as JSON
    /// 2. Otherwise, create an empty parameter object and populate properties from individual arguments using Fasterflect
    /// </summary>
    /// <param name="functionName">The name of the function to get parameter type for</param>
    /// <param name="arguments">Dictionary of function call arguments</param>
    /// <returns>Deserialized or constructed parameter object cast to NextStep</returns>
    public NextStep? CreateParameterFromArguments(string functionName, IDictionary<string, object?> arguments)
    {
        if (!_functions.TryGetValue(functionName, out var pair) || pair.ParameterType == null)
        {
            return null;
        }

        // First, check if we have a "nextStep" argument (existing behavior)
        if (arguments.TryGetValue("nextStep", out var nextStepValue) && nextStepValue is string nextStepJson)
        {
            return DeserializeArgument(functionName, nextStepJson);
        }

        // If no "nextStep" argument, create empty parameter object and populate from individual arguments

        // Create an instance of the parameter type using Fasterflect
        var parameterInstance = pair.ParameterType.CreateInstance();
        if (parameterInstance == null)
        {
            return null;
        }

        // Cycle through all argument keys and set corresponding properties
        foreach (var argument in arguments)
        {
            if (argument.Value == null) continue;

            try
            {
                // Get the property info to determine the target type
                var propertyInfo = pair.ParameterType.Property(argument.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (propertyInfo == null) continue;

                // Convert the argument value to the correct type if needed
                var convertedValue = ConvertArgumentValue(argument.Value, propertyInfo.PropertyType);
                
                // Use Fasterflect to set the property (case-insensitive property lookup)
                parameterInstance.SetPropertyValue(argument.Key, convertedValue);
            }
            catch (Exception)
            {
                // Skip this property if setting fails
                continue;
            }
        }

        return (NextStep) parameterInstance;
    }

    /// <summary>
    /// **Converts argument values to the target property type**.
    /// 
    /// Handles common type conversions needed for function call arguments,
    /// especially string to primitive type conversions.
    /// </summary>
    /// <param name="argumentValue">The raw argument value from function call</param>
    /// <param name="targetType">The target property type</param>
    /// <returns>Converted value or original value if no conversion needed</returns>
    private object? ConvertArgumentValue(object argumentValue, Type targetType)
    {
        if (argumentValue == null)
        {
            return null;
        }

        // If types already match, return as-is
        if (targetType.IsAssignableFrom(argumentValue.GetType()))
        {
            return argumentValue;
        }

        // Handle nullable types
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType)!;
            return ConvertArgumentValue(argumentValue, underlyingType);
        }

        // Handle JsonElement arrays for List<string> conversion
        if (targetType == typeof(List<string>) && argumentValue is System.Text.Json.JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
        {
            var stringList = new List<string>();
            foreach (var element in jsonArray.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    stringList.Add(element.GetString() ?? string.Empty);
                }
            }
            return stringList;
        }

        // Handle string conversions to common types
        if (argumentValue is string stringValue && !string.IsNullOrEmpty(stringValue))
        {
            try
            {
                if (targetType == typeof(bool))
                {
                    return bool.Parse(stringValue);
                }

                if (targetType == typeof(int))
                {
                    return int.Parse(stringValue);
                }

                if (targetType == typeof(decimal))
                {
                    return decimal.Parse(stringValue);
                }

                if (targetType == typeof(double))
                {
                    return double.Parse(stringValue);
                }

                if (targetType == typeof(float))
                {
                    return float.Parse(stringValue);
                }

                if (targetType == typeof(long))
                {
                    return long.Parse(stringValue);
                }

                if (targetType == typeof(DateTime))
                {
                    return DateTime.Parse(stringValue);
                }

                if (targetType == typeof(Guid))
                {
                    return Guid.Parse(stringValue);
                }

                if (targetType.IsEnum)
                {
                    return Enum.Parse(targetType, stringValue, true);
                }
            }
            catch (Exception)
            {
                // If parsing fails, try using Convert.ChangeType as fallback
                try
                {
                    return Convert.ChangeType(stringValue, targetType);
                }
                catch (Exception)
                {
                    // Return original value if all conversions fail
                    return argumentValue;
                }
            }
        }

        // For non-string values, try Convert.ChangeType as fallback
        try
        {
            return Convert.ChangeType(argumentValue, targetType);
        }
        catch (Exception)
        {
            // Return original value if conversion fails
            return argumentValue;
        }
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
            PropertyNameResolver = PropertyNameResolvers.CamelCase
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
    /// Recursively sets additionalProperties to false and makes all properties required for all object types in the schema.
    /// This ensures OpenAI compatibility by preventing additional properties and requiring all defined properties.
    /// </summary>
    private static void SetAdditionalPropertiesFalseRecursively(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            // If this object has type "object", set additionalProperties to false
            if (obj.ContainsKey("type") && obj["type"]?.GetValue<string>() == "object")
            {
                obj["additionalProperties"] = false;

                // If this object has properties, make all of them required
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