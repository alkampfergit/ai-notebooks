using System.Collections.Concurrent;

namespace SkPlayground.Models;

/// <summary>
/// Represents a collection of SQL query execution results stored in the conversation state.
/// This class maintains a dictionary of query results indexed by result ID, allowing multiple
/// query results to be cached and accessed throughout the conversation.
/// </summary>
/// <remarks>
/// The query result collection is typically stored in the state manager using the key
/// "sql_query_result_collection". Each query result is stored with its result ID as the key,
/// enabling quick lookup and preventing redundant query executions.
///
/// **Thread Safety:** Uses `ConcurrentDictionary` to ensure thread-safe operations when adding
/// or retrieving query results from multiple concurrent operations.
///
/// **Usage Example:**
/// ```csharp
/// // Retrieve the collection from state
/// var collection = StateManager.GetMemoryValue&lt;SqlQueryResultCollection&gt;("sql_query_result_collection");
///
/// // Add a new query result
/// collection.AddResult(executionResult);
///
/// // Get a specific result by ID
/// if (collection.TryGetResult("result_123", out var result))
/// {
///     // Use the result...
/// }
/// ```
/// </remarks>
public sealed class SqlQueryResultCollection
{
    /// <summary>
    /// Internal dictionary storing query results indexed by result ID.
    /// Uses case-insensitive comparison for result IDs.
    /// </summary>
    private readonly ConcurrentDictionary<string, SqlQueryExecutionResult> _results =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the total number of query results currently stored in the collection.
    /// </summary>
    public int Count => _results.Count;

    /// <summary>
    /// Gets a read-only collection of all result IDs that have been stored.
    /// </summary>
    /// <value>
    /// A collection of result IDs (case-insensitive) that can be used to enumerate
    /// all cached query results.
    /// </value>
    public IReadOnlyCollection<string> ResultIds => _results.Keys.ToList();

    /// <summary>
    /// Adds or updates a SQL query execution result in the collection.
    /// </summary>
    /// <param name="result">The SQL query execution result to add or update.</param>
    /// <returns>
    /// Returns `true` if this is a new result being added, `false` if an existing result
    /// for the same result ID is being updated.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    /// <remarks>
    /// If a result with the specified result ID already exists, it will be replaced with the new result.
    /// </remarks>
    public bool AddResult(SqlQueryExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        bool isNew = !_results.ContainsKey(result.ResultId);
        _results[result.ResultId] = result;
        return isNew;
    }

    /// <summary>
    /// Attempts to retrieve a SQL query execution result from the collection.
    /// </summary>
    /// <param name="resultId">The result ID of the query (case-insensitive).</param>
    /// <param name="result">
    /// When this method returns, contains the query execution result if found; otherwise, null.
    /// </param>
    /// <returns>
    /// `true` if the result was found in the collection; otherwise, `false`.
    /// </returns>
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

    /// <summary>
    /// Retrieves a SQL query execution result from the collection.
    /// </summary>
    /// <param name="resultId">The result ID of the query (case-insensitive).</param>
    /// <returns>The SQL query execution result if found.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no result exists for the specified result ID.
    /// </exception>
    public SqlQueryExecutionResult GetResult(string resultId)
    {
        if (!_results.TryGetValue(resultId, out var result))
        {
            throw new KeyNotFoundException($"No query result found with ID '{resultId}'.");
        }

        return result;
    }

    /// <summary>
    /// Checks if a result exists for the specified result ID.
    /// </summary>
    /// <param name="resultId">The result ID of the query (case-insensitive).</param>
    /// <returns>`true` if a result exists; otherwise, `false`.</returns>
    public bool ContainsResult(string resultId)
    {
        return _results.ContainsKey(resultId);
    }

    /// <summary>
    /// Gets the timestamp when a specific query result was executed.
    /// </summary>
    /// <param name="resultId">The result ID of the query (case-insensitive).</param>
    /// <returns>
    /// The UTC DateTime when the query was executed, or null if the result is not found.
    /// </returns>
    public DateTime? GetExecutedAtUtc(string resultId)
    {
        return _results.TryGetValue(resultId, out var result)
            ? result.ExecutedAtUtc
            : null;
    }

    /// <summary>
    /// Gets the database name for a specific query result.
    /// </summary>
    /// <param name="resultId">The result ID of the query (case-insensitive).</param>
    /// <returns>
    /// The database name where the query was executed, or null if the result is not found.
    /// </returns>
    public string? GetDatabaseName(string resultId)
    {
        return _results.TryGetValue(resultId, out var result)
            ? result.DatabaseName
            : null;
    }

    /// <summary>
    /// Removes a query result from the collection.
    /// </summary>
    /// <param name="resultId">The result ID of the query to remove (case-insensitive).</param>
    /// <returns>`true` if the result was removed; `false` if it was not found.</returns>
    public bool RemoveResult(string resultId)
    {
        return _results.TryRemove(resultId, out _);
    }

    /// <summary>
    /// Clears all query results from the collection.
    /// </summary>
    public void Clear()
    {
        _results.Clear();
    }

    /// <summary>
    /// Gets all query results currently stored in the collection.
    /// </summary>
    /// <returns>A read-only list of all SQL query execution results.</returns>
    public IReadOnlyList<SqlQueryExecutionResult> GetAllResults()
    {
        return _results.Values.ToList();
    }

    /// <summary>
    /// Gets all query results for a specific database.
    /// </summary>
    /// <param name="databaseName">The database name to filter by (case-insensitive).</param>
    /// <returns>A read-only list of query results executed on the specified database.</returns>
    public IReadOnlyList<SqlQueryExecutionResult> GetResultsByDatabase(string databaseName)
    {
        return _results.Values
            .Where(r => string.Equals(r.DatabaseName, databaseName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
