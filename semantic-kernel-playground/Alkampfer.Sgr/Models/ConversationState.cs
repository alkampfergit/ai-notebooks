using System.Collections.Concurrent;

namespace Alkampfer.Sgr.Models;

/// <summary>
/// Represents the state of a conversation, including token usage and memory.
/// </summary>
public class ConversationState
{
    /// <summary>
    /// Total number of input tokens used in the conversation.
    /// </summary>
    public int TotalInputTokens { get; set; }

    /// <summary>
    /// Total number of output tokens used in the conversation.
    /// </summary>
    public int TotalOutputTokens { get; set; }

    /// <summary>
    /// A thread-safe dictionary to store conversation memories and related data.
    /// </summary>
    public ConcurrentDictionary<string, object> Memory { get; set; } = new();
}
