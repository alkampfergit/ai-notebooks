using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using SkPlayground.BusinessFunctions;

namespace SkPlayground.Models;

[Description("Represents the next step in a task workflow")]
public class NextStep
{
    [Description("Current status of the workflow")]
    public required string CurrentState { get; set; }

    [Description("Brief list of next step planned")]
    public required List<string> PlanRemainingStepsBrief { get; set; }

    [Description("Indicates if the task is completed")]
    public bool TaskCompleted { get; set; } = false;

    [Description("Tool to call for next action")]
    public required ToolCall ToolCall { get; set; }
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