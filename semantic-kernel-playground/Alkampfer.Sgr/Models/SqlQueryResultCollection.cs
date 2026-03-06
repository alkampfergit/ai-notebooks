using System.Collections.Concurrent;

namespace Alkampfer.Sgr.Models;

/// <summary>
/// Represents a collection of SQL query execution results stored in the conversation state.
/// </summary>
public sealed class SqlQueryResultCollection
{
    private readonly ConcurrentDictionary<string, SqlQueryExecutionResult> _results =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _results.Count;

    public IReadOnlyCollection<string> ResultIds => _results.Keys.ToList();

    public bool AddResult(SqlQueryExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        bool isNew = !_results.ContainsKey(result.ResultId);
        _results[result.ResultId] = result;
        return isNew;
    }

    public bool TryGetResult(string resultId, out SqlQueryExecutionResult? result)
    {
        if (_results.TryGetValue(resultId, out var executionResult))
        {
            result = executionResult;
            return true;
        }

        result = null;
        return false;
    }

    public SqlQueryExecutionResult GetResult(string resultId)
    {
        if (!_results.TryGetValue(resultId, out var result))
        {
            throw new KeyNotFoundException($"No query result found with ID '{resultId}'.");
        }

        return result;
    }

    public bool ContainsResult(string resultId) => _results.ContainsKey(resultId);

    public DateTime? GetExecutedAtUtc(string resultId)
    {
        return _results.TryGetValue(resultId, out var result) ? result.ExecutedAtUtc : null;
    }

    public string? GetDatabaseName(string resultId)
    {
        return _results.TryGetValue(resultId, out var result) ? result.DatabaseName : null;
    }

    public bool RemoveResult(string resultId) => _results.TryRemove(resultId, out _);

    public void Clear() => _results.Clear();

    public IReadOnlyList<SqlQueryExecutionResult> GetAllResults() => _results.Values.ToList();

    public IReadOnlyList<SqlQueryExecutionResult> GetResultsByDatabase(string databaseName)
    {
        return _results.Values
            .Where(r => string.Equals(r.DatabaseName, databaseName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
