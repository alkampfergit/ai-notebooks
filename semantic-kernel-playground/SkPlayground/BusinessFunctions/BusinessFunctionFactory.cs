using Fasterflect;
using Microsoft.SemanticKernel;
using SkPlayground.Models;
using SkPlayground.Services;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// **Record containing both business function and kernel function instances**
/// 
/// This record pairs together:
/// - **BusinessFunction**: The concrete business logic implementation
/// - **KernelFunction**: The Semantic Kernel function wrapper for LLM integration
/// - **ParameterType**: The CLR Type used for deserializing the function argument
/// 
/// This enables both direct business function calls and LLM-driven function calls
/// while maintaining consistency between the two execution paths.
/// </summary>
/// <param name="BusinessFunction">The concrete business function instance</param>
/// <param name="KernelFunction">The corresponding Semantic Kernel function</param>
/// <param name="ParameterType">The CLR Type of the function parameter (e.g. FooParameters)</param>
public class FunctionInformations
{
    public BusinessFunction BusinessFunction { get; set; }
    public KernelFunction KernelFunction { get; set; }
    public Type ParameterType { get; set; }

    public FunctionInformations(BusinessFunction businessFunction, KernelFunction kernelFunction, Type parameterType)
    {
        BusinessFunction = businessFunction;
        KernelFunction = kernelFunction;
        ParameterType = parameterType;
    }
}

/// <summary>
/// **Factory for creating and managing business function instances and Kernel functions**
/// 
/// This factory provides a centralized way to:
/// - Create instances of all business functions with proper dependencies
/// - Generate corresponding Semantic Kernel functions from business functions
/// - Maintain consistency between parameter types and function implementations
/// - Store function pairs in a dictionary for easy access by name
/// - Enable dependency injection and configuration management
/// </summary>
public class BusinessFunctionFactory
{
    private readonly Dictionary<string, FunctionInformations> _functions;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// **Dictionary containing all registered functions** indexed by function name.
    /// Each entry contains both the business function instance and its corresponding kernel function.
    /// </summary>
    public IReadOnlyDictionary<string, FunctionInformations> Functions => _functions;

    /// <summary>
    /// **Constructor that initializes all business functions and their kernel function counterparts**.
    /// 
    /// Creates all function instances with proper dependencies and generates corresponding
    /// Semantic Kernel functions for LLM integration.
    /// </summary>
    /// <param name="jsonOptions">JSON serialization options for parameter handling</param>
    /// <param name="databaseService">Database service instance for all functions</param>
    public BusinessFunctionFactory(JsonSerializerOptions jsonOptions, DatabaseService databaseService)
    {
        _jsonOptions = jsonOptions;
        _functions = CreateAllFunctions(jsonOptions, databaseService);
    }

    /// <summary>
    /// **Creates all business function instances** with shared dependencies.
    /// 
    /// This method instantiates all concrete business functions with:
    /// - Shared JSON serialization options for consistency
    /// - Database service for data operations
    /// - Proper dependency injection pattern
    /// </summary>
    /// <param name="jsonOptions">JSON serialization options for parameter handling</param>
    /// <param name="databaseService">Database service instance for all functions</param>
    /// <returns>Dictionary mapping function names to function pairs</returns>
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

        // Create lambda functions that are empty
        var reportTaskCompletion = [Description("Conclude the process with a summary")]
        (ReportTaskCompletionParameters nextStep) =>
        { };

        var sendEmail = [Description("Sends an email with optional file attachments")]
        (SendEmailParameters nextStep) =>
        { };

        var issueInvoice = [Description("Issues an invoice for specified products with optional discount")]
        (IssueInvoiceParameters nextStep) =>
        { };

        var getCustomerData = [Description("Retrieves customer data by email address")]
        (GetCustomerDataParameters nextStep) =>
        { };

        var voidInvoice = [Description("Voids an existing invoice with a reason")]
        (VoidInvoiceParameters nextStep) =>
        { };

        var createRule = [Description("Creates a rule for a specific customer")]
        (CreateRuleParameters nextStep) =>
        { };

        // Create kernel functions from lambda functions
        var reportTaskCompletionKernel = KernelFunctionFactory.CreateFromMethod(reportTaskCompletion, "reportTaskCompletion");
        var sendEmailKernel = KernelFunctionFactory.CreateFromMethod(sendEmail, "sendEmail");
        var issueInvoiceKernel = KernelFunctionFactory.CreateFromMethod(issueInvoice, "issueInvoice");
        var getCustomerDataKernel = KernelFunctionFactory.CreateFromMethod(getCustomerData, "getCustomerData");
        var voidInvoiceKernel = KernelFunctionFactory.CreateFromMethod(voidInvoice, "voidInvoice");
        var createRuleKernel = KernelFunctionFactory.CreateFromMethod(createRule, "createRule");

        // Return dictionary with function pairs (also store parameter types)
        return new Dictionary<string, FunctionInformations>
        {
            ["reportTaskCompletion"] = new FunctionInformations(reportTaskCompletionFunc, reportTaskCompletionKernel, typeof(ReportTaskCompletionParameters)),
            ["sendEmail"] = new FunctionInformations(sendEmailFunc, sendEmailKernel, typeof(SendEmailParameters)),
            ["issueInvoice"] = new FunctionInformations(issueInvoiceFunc, issueInvoiceKernel, typeof(IssueInvoiceParameters)),
            ["getCustomerData"] = new FunctionInformations(getCustomerDataFunc, getCustomerDataKernel, typeof(GetCustomerDataParameters)),
            ["voidInvoice"] = new FunctionInformations(voidInvoiceFunc, voidInvoiceKernel, typeof(VoidInvoiceParameters)),
            ["createRule"] = new FunctionInformations(createRuleFunc, createRuleKernel, typeof(CreateRuleParameters))
        };
    }

    /// <summary>
    /// **Gets all Kernel functions** for Semantic Kernel integration.
    /// 
    /// This method extracts all KernelFunction instances from the function pairs
    /// and returns them as a list for use with Semantic Kernel's function calling.
    /// </summary>
    /// <returns>List of KernelFunction instances ready for LLM integration</returns>
    public List<KernelFunction> GetAllKernelFunctions()
    {
        return _functions.Values.Select(pair => pair.KernelFunction).ToList();
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
    /// **Gets a specific function pair** by name.
    /// 
    /// This method allows access to both the business function and kernel function
    /// for a specific function by its registered name.
    /// </summary>
    /// <param name="functionName">The name of the function to retrieve</param>
    /// <returns>FunctionPair containing both business and kernel functions, or null if not found</returns>
    public FunctionInformations? GetFunction(string functionName)
    {
        return _functions.TryGetValue(functionName, out var pair) ? pair : null;
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
}