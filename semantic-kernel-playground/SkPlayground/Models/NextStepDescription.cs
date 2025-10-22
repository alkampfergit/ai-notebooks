using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SkPlayground.Models;

[Description("Next step to execute with summary")]
public class NextStepDescription
{
    [Description("What to execute next to move on in the workflow")]
    public required ToolCall NextStep { get; set; }

    [Description("Current status of the workflow")]
    public required string CurrentState { get; set; }

    [Description("Brief list of next step planned with corresponding tool name")]
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