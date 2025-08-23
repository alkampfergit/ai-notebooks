using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SkPlayground.Models;

public record NextStep
{
    [JsonPropertyName("current_state")]
    [Required]
    public required string CurrentState { get; init; }
    
    [JsonPropertyName("plan_remaining_steps_brief")]
    [Required]
    [MinLength(1)]
    [MaxLength(5)]
    public required List<string> PlanRemainingStepsBrief { get; init; }
    
    [JsonPropertyName("task_completed")]
    public bool TaskCompleted { get; init; } = false;
    
    [JsonPropertyName("function")]
    [Required]
    public required ToolFunction Function { get; init; }
}