using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SkPlayground.Models;

[Description("Next step to execute with summary")]
public class NextStep
{
    [Description("execute first remaining step")]
    public required ToolCall Function { get; set; }

    [Description("Current status of the workflow")]
    public required string CurrentState { get; set; }

    [Description("Brief list of next step planned")]
    public required List<string> PlanRemainingStepsBrief { get; set; }

    [Description("Indicates if the task is completed")]
    public bool TaskCompleted { get; set; } = false;
}

public abstract class ToolCall
{
    /// <summary>
    /// Discriminator property to identify the tool call type
    /// </summary>
    [JsonProperty("type")]
    [Required]
    public abstract string Type { get; }
}