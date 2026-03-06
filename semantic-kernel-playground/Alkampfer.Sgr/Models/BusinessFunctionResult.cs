namespace Alkampfer.Sgr.Models;

/// <summary>
/// Standardized result type for all business functions containing both
/// the actual result object and a human-readable summary for LLM conversation
/// </summary>
/// <param name="Result">The actual result object returned by the business function</param>
/// <param name="Summary">Human-readable description of what was accomplished for LLM conversation</param>
public readonly record struct BusinessFunctionResult(object? Result, string Summary);
