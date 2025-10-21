using SkPlayground.Models;
using System.Collections.Concurrent;

namespace SkPlayground.Services;

/// <summary>
/// Manages conversation state using AsyncLocal storage for async flow context isolation.
/// </summary>
public static class StateManager
{
    private static readonly AsyncLocal<ConversationState?> _asyncLocalState = new();

    /// <summary>
    /// Initializes a new conversation state in the current async context.
    /// </summary>
    /// <returns>The newly created conversation state.</returns>
    public static ConversationState Start()
    {
        var state = new ConversationState();
        _asyncLocalState.Value = state;
        return state;
    }

    /// <summary>
    /// Gets the current conversation state, or null if not initialized.
    /// </summary>
    /// <returns>The current conversation state or null.</returns>
    public static ConversationState? GetCurrent()
    {
        return _asyncLocalState.Value;
    }

    /// <summary>
    /// Clears the current conversation state.
    /// </summary>
    public static void Clear()
    {
        _asyncLocalState.Value = null;
    }

    /// <summary>
    /// Gets the total number of input tokens.
    /// </summary>
    /// <returns>The total input tokens, or 0 if state is not initialized.</returns>
    public static int GetTotalInputTokens()
    {
        return _asyncLocalState.Value?.TotalInputTokens ?? 0;
    }

    /// <summary>
    /// Sets the total number of input tokens.
    /// </summary>
    /// <param name="tokens">The number of tokens to set.</param>
    /// <exception cref="InvalidOperationException">Thrown when state is not initialized.</exception>
    public static void SetTotalInputTokens(int tokens)
    {
        EnsureStateInitialized();
        _asyncLocalState.Value!.TotalInputTokens = tokens;
    }

    /// <summary>
    /// Adds to the total number of input tokens.
    /// </summary>
    /// <param name="tokens">The number of tokens to add.</param>
    /// <exception cref="InvalidOperationException">Thrown when state is not initialized.</exception>
    public static void AddInputTokens(int tokens)
    {
        EnsureStateInitialized();
        _asyncLocalState.Value!.TotalInputTokens += tokens;
    }

    /// <summary>
    /// Gets the total number of output tokens.
    /// </summary>
    /// <returns>The total output tokens, or 0 if state is not initialized.</returns>
    public static int GetTotalOutputTokens()
    {
        return _asyncLocalState.Value?.TotalOutputTokens ?? 0;
    }

    /// <summary>
    /// Sets the total number of output tokens.
    /// </summary>
    /// <param name="tokens">The number of tokens to set.</param>
    /// <exception cref="InvalidOperationException">Thrown when state is not initialized.</exception>
    public static void SetTotalOutputTokens(int tokens)
    {
        EnsureStateInitialized();
        _asyncLocalState.Value!.TotalOutputTokens = tokens;
    }

    /// <summary>
    /// Adds to the total number of output tokens.
    /// </summary>
    /// <param name="tokens">The number of tokens to add.</param>
    /// <exception cref="InvalidOperationException">Thrown when state is not initialized.</exception>
    public static void AddOutputTokens(int tokens)
    {
        EnsureStateInitialized();
        _asyncLocalState.Value!.TotalOutputTokens += tokens;
    }

    /// <summary>
    /// Gets the memory dictionary.
    /// </summary>
    /// <returns>The memory dictionary, or an empty dictionary if state is not initialized.</returns>
    public static ConcurrentDictionary<string, object> GetMemory()
    {
        return _asyncLocalState.Value?.Memory ?? new ConcurrentDictionary<string, object>();
    }

    /// <summary>
    /// Gets a memory value by key.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The value if found, otherwise null.</returns>
    public static object? GetMemoryValue(string key)
    {
        return _asyncLocalState.Value?.Memory.TryGetValue(key, out var value) == true ? value : null;
    }

    /// <summary>
    /// Gets a memory value by key with a specific type.
    /// </summary>
    /// <typeparam name="T">The type to cast the value to.</typeparam>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The value if found and can be cast, otherwise default(T).</returns>
    public static T? GetMemoryValue<T>(string key)
    {
        var value = GetMemoryValue(key);
        if (value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <summary>
    /// Sets a memory value.
    /// </summary>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to store.</param>
    /// <exception cref="InvalidOperationException">Thrown when state is not initialized.</exception>
    public static void SetMemoryValue(string key, object value)
    {
        EnsureStateInitialized();
        _asyncLocalState.Value!.Memory[key] = value;
    }

    /// <summary>
    /// Tries to get a memory value by key.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <param name="value">The output value if found.</param>
    /// <returns>True if the key was found, otherwise false.</returns>
    public static bool TryGetMemoryValue(string key, out object? value)
    {
        if (_asyncLocalState.Value?.Memory.TryGetValue(key, out value) == true)
        {
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>
    /// Tries to get a memory value by key with a specific type.
    /// </summary>
    /// <typeparam name="T">The type to cast the value to.</typeparam>
    /// <param name="key">The key to retrieve.</param>
    /// <param name="value">The output value if found and can be cast.</param>
    /// <returns>True if the key was found and the value can be cast to T, otherwise false.</returns>
    public static bool TryGetMemoryValue<T>(string key, out T? value)
    {
        if (TryGetMemoryValue(key, out var objValue) && objValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Removes a memory value by key.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>True if the key was found and removed, otherwise false.</returns>
    public static bool RemoveMemoryValue(string key)
    {
        return _asyncLocalState.Value?.Memory.TryRemove(key, out _) == true;
    }

    /// <summary>
    /// Checks if a memory key exists.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key exists, otherwise false.</returns>
    public static bool ContainsMemoryKey(string key)
    {
        return _asyncLocalState.Value?.Memory.ContainsKey(key) == true;
    }

    /// <summary>
    /// Clears all memory values.
    /// </summary>
    public static void ClearMemory()
    {
        _asyncLocalState.Value?.Memory.Clear();
    }

    /// <summary>
    /// Gets the count of memory entries.
    /// </summary>
    /// <returns>The number of memory entries, or 0 if state is not initialized.</returns>
    public static int GetMemoryCount()
    {
        return _asyncLocalState.Value?.Memory.Count ?? 0;
    }

    private static void EnsureStateInitialized()
    {
        if (_asyncLocalState.Value == null)
        {
            throw new InvalidOperationException("Conversation state has not been initialized. Call Start() first.");
        }
    }
}
