using Alkampfer.Sgr.Models;

namespace Alkampfer.Sgr.BusinessFunctions;

/// <summary>
/// **Non-generic base class for all business functions** that provides a unified interface
/// for executing business operations with JSON parameter handling.
///
/// This abstract class enables:
/// - **Polymorphic execution** through common base type
/// - **Dynamic dispatch** without knowing specific parameter types
/// - **Framework integration** for dependency injection and factory patterns
/// - **Consistent interface** across all business functions
/// - **Availability checking** to determine if a function should be exposed to the LLM
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

    /// <summary>
    /// **Virtual method to determine if this business function is currently available for execution.**
    ///
    /// This method enables dynamic function availability based on runtime conditions such as:
    /// - **State-based availability**: Function is only available after certain prerequisites are met
    /// - **Context-dependent availability**: Function requires specific data to be present in state manager
    /// - **Conditional logic**: Function may only be relevant in certain scenarios
    /// - **Progressive disclosure**: Functions become available as the conversation progresses
    ///
    /// **Default Behavior:** Returns `true`, meaning the function is always available.
    /// </summary>
    /// <returns>
    /// `true` if the function should be included in the schema and made available to the LLM;
    /// `false` if the function should be excluded from the current schema generation.
    /// </returns>
    public virtual bool IsAvailable()
    {
        return true;
    }
}

/// <summary>
/// **Generic base class for typed business functions** that provides strongly-typed
/// parameter handling while inheriting from the non-generic base.
/// </summary>
/// <typeparam name="T">The parameter type for this business function</typeparam>
public abstract class BusinessFunction<T> : BusinessFunction where T : class
{
    /// <summary>
    /// **Execute method that accepts a ToolCall instance** and casts it to the specific type T.
    /// </summary>
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
    protected abstract Task<BusinessFunctionResult> ExecuteAsync(T parameters, CancellationToken cancellationToken = default);
}
