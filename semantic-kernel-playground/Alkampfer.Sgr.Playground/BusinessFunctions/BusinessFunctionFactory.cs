using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Alkampfer.Sgr.Models;
using Alkampfer.Sgr.BusinessFunctions;
using Alkampfer.Sgr.Utils;
using Alkampfer.Sgr.Playground.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Alkampfer.Sgr.Playground.BusinessFunctions;

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
/// - Create instances of business functions with proper dependencies
/// - Generate JSON schemas for function parameters using PolymorphicSchemaManager
/// - Maintain consistency between parameter types and function implementations
/// - Store function information in a dictionary for easy access by name
/// - Enable dependency injection and configuration management
/// - Support structured LLM responses with JSON schema validation and polymorphic deserialization
/// - Support dynamic composition by allowing caller to specify which functions to include
/// </summary>
public class BusinessFunctionFactory
{
    private readonly Dictionary<string, FunctionInformations> _functions;
    private readonly PolymorphicSchemaManager<NextStep, ToolCall> _schemaManager;
    private readonly DatabaseService _databaseService;
    private readonly SqlServerService _sqlServerService;
    private readonly Kernel _kernel;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// **Constructor that initializes business functions with polymorphic schema support**.
    ///
    /// Creates function instances based on the provided ToolCall types with proper dependencies
    /// and configures the PolymorphicSchemaManager for NextStep/ToolCall polymorphic handling
    /// and JSON schema generation.
    /// </summary>
    /// <param name="databaseService">Database service instance for all functions</param>
    /// <param name="sqlServerService">SQL Server service instance for SQL-related functions</param>
    /// <param name="kernel">Semantic Kernel instance for LLM operations</param>
    /// <param name="loggerFactory">Logger factory for creating typed loggers</param>
    /// <param name="toolCallTypes">Array of ToolCall types to include in the factory. If null or empty, includes all available types.</param>
    public BusinessFunctionFactory(
        DatabaseService databaseService,
        SqlServerService sqlServerService,
        Kernel kernel,
        ILoggerFactory loggerFactory,
        Type[] toolCallTypes)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _sqlServerService = sqlServerService ?? throw new ArgumentNullException(nameof(sqlServerService));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        if (toolCallTypes?.Any() != true)
        {
            throw new ArgumentException("At least one ToolCall type must be provided", nameof(toolCallTypes));
        }

        // **Initialize PolymorphicSchemaManager with specified ToolCall derived types**
        _schemaManager = new PolymorphicSchemaManager<NextStep, ToolCall>("type").AddDerivedTypes(toolCallTypes);

        _functions = new Dictionary<string, FunctionInformations>();

        // **Create function instances for the specified types**
        AddFunctionTypes(toolCallTypes);
    }

    /// <summary>
    /// **Adds business functions for the specified ToolCall types**.
    ///
    /// This method dynamically creates BusinessFunction instances and their schemas
    /// based on the provided ToolCall types. It uses a mapping convention where:
    /// - ToolCall type name ends with "ToolCall" (e.g., SendEmailToolCall)
    /// - BusinessFunction type name ends with "Function" (e.g., SendEmailFunction)
    /// - Function name in dictionary is camelCase without suffix (e.g., "sendEmail")
    /// </summary>
    /// <param name="toolCallTypes">The ToolCall types to add to the factory</param>
    public void AddFunctionTypes(params Type[] toolCallTypes)
    {
        foreach (var toolCallType in toolCallTypes)
        {
            // **Validate that the type is a ToolCall**
            if (!typeof(ToolCall).IsAssignableFrom(toolCallType))
            {
                throw new ArgumentException($"Type {toolCallType.Name} must derive from ToolCall", nameof(toolCallTypes));
            }

            // **Derive the function name and BusinessFunction type name**
            // Example: SendEmailToolCall -> sendEmail, SendEmailFunction
            var functionName = GetFunctionName(toolCallType);
            var businessFunctionType = GetBusinessFunctionType(toolCallType);

            // **Create the BusinessFunction instance with appropriate dependencies**
            var businessFunction = CreateBusinessFunctionInstance(businessFunctionType);

            // **Generate JSON schema for this specific ToolCall type**
            var jsonSchema = JsonNode.Parse(_schemaManager.GenerateSchema(new[] { toolCallType }))!;

            // **Add to the functions dictionary**
            _functions[functionName] = new FunctionInformations(businessFunction, toolCallType, jsonSchema);

            // **Add the type to the schema manager if not already present**
            _schemaManager.AddDerivedTypes(toolCallType);
        }
    }

    /// <summary>
    /// **Derives the function name from a ToolCall type**.
    ///
    /// Converts "SendEmailToolCall" to "sendEmail" by:
    /// 1. Removing the "ToolCall" suffix
    /// 2. Converting to camelCase
    /// </summary>
    /// <param name="toolCallType">The ToolCall type</param>
    /// <returns>The function name in camelCase</returns>
    private static string GetFunctionName(Type toolCallType)
    {
        var name = toolCallType.Name;
        if (name.EndsWith("ToolCall"))
        {
            name = name.Substring(0, name.Length - "ToolCall".Length);
        }

        // Convert to camelCase (first letter lowercase)
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// **Derives the BusinessFunction type from a ToolCall type**.
    ///
    /// Converts "SendEmailToolCall" to SendEmailFunction type by:
    /// 1. Removing the "ToolCall" suffix
    /// 2. Appending "Function" suffix
    /// 3. Looking up the type in the same namespace
    /// </summary>
    /// <param name="toolCallType">The ToolCall type</param>
    /// <returns>The corresponding BusinessFunction type</returns>
    private static Type GetBusinessFunctionType(Type toolCallType)
    {
        var baseName = toolCallType.Name;
        if (baseName.EndsWith("ToolCall"))
        {
            baseName = baseName.Substring(0, baseName.Length - "ToolCall".Length);
        }

        var functionTypeName = $"{toolCallType.Namespace}.{baseName}Function";
        var functionType = toolCallType.Assembly.GetType(functionTypeName);

        if (functionType == null)
        {
            throw new InvalidOperationException(
                $"Could not find BusinessFunction type '{functionTypeName}' for ToolCall type '{toolCallType.Name}'");
        }

        return functionType;
    }

    /// <summary>
    /// **Creates a BusinessFunction instance with proper dependency injection**.
    ///
    /// This method uses reflection to create instances of BusinessFunction types,
    /// analyzing their constructor parameters and providing the appropriate dependencies
    /// (DatabaseService, SqlServerService, Kernel, ILogger, or no dependencies).
    /// </summary>
    /// <param name="businessFunctionType">The BusinessFunction type to instantiate</param>
    /// <returns>An instance of the BusinessFunction</returns>
    private BusinessFunction CreateBusinessFunctionInstance(Type businessFunctionType)
    {
        // **Find the constructor and determine required dependencies**
        var constructors = businessFunctionType.GetConstructors();
        if (constructors.Length == 0)
        {
            throw new InvalidOperationException($"Type {businessFunctionType.Name} has no public constructors");
        }

        var constructor = constructors[0];
        var parameters = constructor.GetParameters();

        // **Build the constructor arguments based on parameter types**
        var args = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            if (paramType == typeof(DatabaseService))
            {
                args[i] = _databaseService;
            }
            else if (paramType == typeof(SqlServerService))
            {
                args[i] = _sqlServerService;
            }
            else if (paramType == typeof(Kernel))
            {
                args[i] = _kernel;
            }
            else if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(ILogger<>))
            {
                // Create typed logger using the generic type argument
                var loggerType = paramType.GetGenericArguments()[0];
                var createLoggerMethod = typeof(LoggerFactoryExtensions)
                    .GetMethod(nameof(LoggerFactoryExtensions.CreateLogger), new[] { typeof(ILoggerFactory) })!
                    .MakeGenericMethod(loggerType);
                args[i] = createLoggerMethod.Invoke(null, new object[] { _loggerFactory });
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unknown dependency type {paramType.Name} for {businessFunctionType.Name}");
            }
        }

        // **Create and return the instance**
        var instance = constructor.Invoke(args);
        if (instance is not BusinessFunction businessFunction)
        {
            throw new InvalidOperationException($"Type {businessFunctionType.Name} is not a BusinessFunction");
        }

        return businessFunction;
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
    /// **Gets the parameter types (ToolCall types) for all currently available business functions.**
    ///
    /// This method filters the registered functions by calling `IsAvailable()` on each business function
    /// and returns only the parameter types for functions that return `true`.
    ///
    /// **Use Cases:**
    /// - Dynamic schema generation based on runtime state
    /// - Progressive function disclosure as conversation evolves
    /// - Context-aware tool availability
    /// </summary>
    /// <returns>Collection of ToolCall types for available functions</returns>
    private IEnumerable<Type> GetAvailableFunctionTypes()
    {
        return _functions.Values
            .Where(fi => fi.BusinessFunction.IsAvailable())
            .Select(fi => fi.ParameterType)
            .Where(t => t != null)
            .Cast<Type>();
    }

    /// <summary>
    /// **Generates comprehensive schema result with documentation** using PolymorphicSchemaManager.
    ///
    /// This method creates an OpenAI-compatible JSON schema definition that includes
    /// **only the currently available** ToolCall types (filtered by `IsAvailable()`) with proper
    /// polymorphic support, discriminators, and comprehensive documentation including outer object
    /// description and tool information.
    ///
    /// **Dynamic Availability:**
    /// Functions are included only if their `IsAvailable()` method returns `true`, enabling
    /// context-aware and state-dependent tool exposure to the LLM.
    /// </summary>
    /// <returns>SchemaGenerationResult containing JSON schema, outer object description, and available tools</returns>
    public SchemaGenerationResult GenerateSchemaWithDocumentationForToolCall()
    {
        var availableTypes = GetAvailableFunctionTypes();
        return _schemaManager.GenerateSchemaWithDocumentation(availableTypes);
    }

    /// <summary>
    /// **Generates comprehensive schema result with documentation for specific tool types** using PolymorphicSchemaManager.
    ///
    /// This method creates an OpenAI-compatible JSON schema definition that includes
    /// only the specified ToolCall types with proper polymorphic support, discriminators,
    /// and comprehensive documentation including outer object description and tool information.
    /// </summary>
    /// <param name="includedToolTypes">The specific ToolCall types to include in the schema and documentation</param>
    /// <returns>SchemaGenerationResult containing JSON schema, outer object description, and available tools</returns>
    public SchemaGenerationResult GenerateSchemaWithDocumentationForToolCall(IEnumerable<Type> includedToolTypes)
    {
        return _schemaManager.GenerateSchemaWithDocumentation(includedToolTypes);
    }

    /// <summary>
    /// **Generates JSON schema for NextStep type** using PolymorphicSchemaManager.
    ///
    /// This method creates an OpenAI-compatible JSON schema definition that includes
    /// **only the currently available** ToolCall types (filtered by `IsAvailable()`) with proper
    /// polymorphic support and discriminators. The schema is automatically configured with
    /// additionalProperties: false and proper const/enum discriminators for each ToolCall type.
    ///
    /// **Dynamic Availability:**
    /// Functions are included only if their `IsAvailable()` method returns `true`, enabling
    /// context-aware and state-dependent tool exposure to the LLM.
    ///
    /// NOTE: For enhanced documentation capabilities, use GenerateSchemaWithDocumentationForToolCall() instead.
    /// </summary>
    /// <returns>JSON schema string representing the NextStep structure with polymorphic ToolCall support</returns>
    public string GenerateJsonSchemaForToolCall()
    {
        var availableTypes = GetAvailableFunctionTypes();
        return _schemaManager.GenerateSchema(availableTypes);
    }

    /// <summary>
    /// **Generates JSON schema for NextStep type with specific tool types** using PolymorphicSchemaManager.
    ///
    /// This method creates an OpenAI-compatible JSON schema definition that includes
    /// only the specified ToolCall types with proper polymorphic support and discriminators.
    /// Useful for scenarios where only a subset of tools should be available.
    /// </summary>
    /// <param name="includedToolTypes">The specific ToolCall types to include in the schema</param>
    /// <returns>JSON schema string representing the NextStep structure with specified ToolCall types</returns>
    public string GenerateJsonSchemaForToolCall(IEnumerable<Type> includedToolTypes)
    {
        return _schemaManager.GenerateSchema(includedToolTypes);
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
