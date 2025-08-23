using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SkPlayground.Models;

[Description("Represents the next step in a task workflow")]
public abstract class NextStep
{
    [Description("Current status of the workflow")]
    public string CurrentState { get; set; }

    [Description("Brief list of next step planned")]
    public List<string> PlanRemainingStepsBrief { get; set; }

    [Description("Indicates if the task is completed")]
    public bool TaskCompleted { get; set; } = false;
}