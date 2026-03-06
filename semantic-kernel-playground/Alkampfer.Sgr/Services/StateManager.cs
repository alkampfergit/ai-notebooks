using Alkampfer.Sgr.Models;
using System.Collections.Concurrent;

namespace Alkampfer.Sgr.Services;

/// <summary>
/// Manages conversation state using AsyncLocal storage for async flow context isolation.
/// </summary>
public static class StateManager
{
    private static readonly AsyncLocal<ConversationState?> _asyncLocalState = new();

    public static ConversationState Start()
    {
        var state = new ConversationState();
        _asyncLocalState.Value = state;
        return state;
    }

    public static ConversationState? GetCurrent() => _asyncLocalState.Value;

    public static void Clear() => _asyncLocalState.Value = null;

    public static int GetTotalInputTokens() => _asyncLocalState.Value?.TotalInputTokens ?? 0;

    public static void SetTotalInputTokens(int tokens)
    {
        EnsureStateInitialized();
        _asyncLocalState.Value!.TotalInputTokens = tokens;
    }

    public static void AddInputTokens(int tokens)
    {
        EnsureStateInitialized();
        _asyncLocalState.Value!.TotalInputTokens += tokens;
    }

    public static int GetTotalOutputTokens() => _asyncLocalState.Value?.TotalOutputTokens ?? 0;

    public static void SetTotalOutputTokens(int tokens)
    {
        EnsureStateInitialized();
        _asyncLocalState.Value!.TotalOutputTokens = tokens;
    }

    public static void AddOutputTokens(int tokens)
    {
        EnsureStateInitialized();
        _asyncLocalState.Value!.TotalOutputTokens += tokens;
    }

    public static ConcurrentDictionary<string, object> GetMemory()
    {
        return _asyncLocalState.Value?.Memory ?? new ConcurrentDictionary<string, object>();
    }

    public static object? GetMemoryValue(string key)
    {
        return _asyncLocalState.Value?.Memory.TryGetValue(key, out var value) == true ? value : null;
    }

    public static T? GetMemoryValue<T>(string key)
    {
        var value = GetMemoryValue(key);
        if (value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    public static void SetMemoryValue(string key, object value)
    {
        EnsureStateInitialized();
        _asyncLocalState.Value!.Memory[key] = value;
    }

    public static bool TryGetMemoryValue(string key, out object? value)
    {
        if (_asyncLocalState.Value?.Memory.TryGetValue(key, out value) == true)
        {
            return true;
        }
        value = null;
        return false;
    }

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

    public static bool HasMemoryType<T>()
    {
        foreach (var item in _asyncLocalState.Value?.Memory ?? new ConcurrentDictionary<string, object>())
        {
            if (item.Value is T)
            {
                return true;
            }
        }
        return false;
    }

    public static bool RemoveMemoryValue(string key)
    {
        return _asyncLocalState.Value?.Memory.TryRemove(key, out _) == true;
    }

    public static bool ContainsMemoryKey(string key)
    {
        return _asyncLocalState.Value?.Memory.ContainsKey(key) == true;
    }

    public static void ClearMemory() => _asyncLocalState.Value?.Memory.Clear();

    public static int GetMemoryCount() => _asyncLocalState.Value?.Memory.Count ?? 0;

    private static void EnsureStateInitialized()
    {
        if (_asyncLocalState.Value == null)
        {
            throw new InvalidOperationException("Conversation state has not been initialized. Call Start() first.");
        }
    }
}
