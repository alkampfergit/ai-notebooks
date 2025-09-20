using System.Text.Json;
using SkPlayground.Models;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// **Non-generic base class for all business functions** that provides a unified interface
/// for executing business operations with JSON parameter handling.
/// 
/// This abstract class enables:
/// - **Polymorphic execution** through common base type
/// - **Dynamic dispatch** without knowing specific parameter types
/// - **Framework integration** for dependency injection and factory patterns
/// - **Consistent interface** across all business functions
/// </summary>
public abstract class BusinessFunction
{
    /// <summary>
    /// **Execute method that accepts a NextStep instance** for direct parameter passing
    /// without JSON serialization. This enables type-safe execution when the parameter
    /// instance is already available.
    /// </summary>
    /// <param name="nextStep">The NextStep instance containing the parameters</param>
    /// <param name="cancellationToken">Cancellation token to support cooperative cancellation</param>
    /// <returns>BusinessFunctionResult containing both result object and summary description</returns>
    public abstract Task<BusinessFunctionResult> ExecuteAsync(ToolCall nextStep, CancellationToken cancellationToken = default);
}

/// <summary>
/// **Generic base class for typed business functions** that provides strongly-typed
/// parameter handling while inheriting from the non-generic base.
/// 
/// This class implements the **Template Method Pattern** where:
/// - The base `Execute(string)` method handles JSON deserialization
/// - Derived classes implement the concrete `Execute(T)` method with typed parameters
/// 
/// ## Architecture Benefits:
/// - **Type Safety**: Each function has strongly-typed parameter classes
/// - **Consistency**: All functions follow the same execution pattern
/// - **JSON Integration**: Seamless integration with JSON-based APIs
/// - **Polymorphism**: Can be used through non-generic base class interface
/// </summary>
/// <typeparam name="T">The parameter type for this business function</typeparam>
public abstract class BusinessFunction<T> : BusinessFunction where T : class
{
    /// <summary>
    /// **Execute method that accepts a ToolCall instance** and casts it to the specific type T.
    /// This provides a way to execute the function with a strongly-typed ToolCall parameter.
    /// </summary>
    /// <param name="toolCall">The ToolCall instance containing the parameters</param>
    /// <param name="cancellationToken">Cancellation token to support cooperative cancellation</param>
    /// <returns>BusinessFunctionResult containing both result object and summary description</returns>
    public override async Task<BusinessFunctionResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default)
    {
        if (toolCall is not T typedParameters)
        {
            throw new ArgumentException($"ToolCall parameter must be of type {typeof(T).Name}", nameof(toolCall));
        }

        return await ExecuteAsync(typedParameters, cancellationToken);
    }

    /// <summary>
    /// **Abstract method** to be implemented by derived classes.
    /// Contains the actual business logic for the specific operation.
    /// </summary>
    /// <param name="parameters">Strongly-typed parameters for the operation</param>
    /// <param name="cancellationToken">Cancellation token to support cooperative cancellation</param>
    /// <returns>BusinessFunctionResult containing both result object and summary description</returns>
    protected abstract Task<BusinessFunctionResult> ExecuteAsync(T parameters, CancellationToken cancellationToken = default);
}