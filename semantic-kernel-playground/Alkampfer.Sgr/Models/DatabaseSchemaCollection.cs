using System.Collections.Concurrent;

namespace Alkampfer.Sgr.Models;

/// <summary>
/// Represents a collection of SQL Server database schemas stored in the conversation state.
/// This class maintains a dictionary of schemas indexed by database name, allowing multiple
/// database schemas to be cached and accessed throughout the conversation.
/// </summary>
/// <remarks>
/// The schema collection is typically stored in the state manager using the key "database_schema_collection".
/// Each database schema is stored with its name as the key, enabling quick lookup and preventing
/// redundant queries to the SQL Server for schema information.
///
/// **Thread Safety:** Uses `ConcurrentDictionary` to ensure thread-safe operations when adding
/// or retrieving schemas from multiple concurrent operations.
/// </remarks>
public sealed class DatabaseSchemaCollection
{
    /// <summary>
    /// Internal dictionary storing schemas indexed by database name.
    /// Uses case-insensitive comparison for database names.
    /// </summary>
    private readonly ConcurrentDictionary<string, DatabaseSchemaEntry> _schemas =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the total number of database schemas currently stored in the collection.
    /// </summary>
    public int Count => _schemas.Count;

    /// <summary>
    /// Gets a read-only collection of all database names that have schemas stored.
    /// </summary>
    public IReadOnlyCollection<string> DatabaseNames => _schemas.Keys.ToList();

    /// <summary>
    /// Adds or updates a database schema in the collection.
    /// </summary>
    public bool AddSchema(SqlDatabaseSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var entry = new DatabaseSchemaEntry
        {
            Schema = schema,
            RetrievedAtUtc = DateTime.UtcNow
        };

        bool isNew = !_schemas.ContainsKey(schema.DatabaseName);
        _schemas[schema.DatabaseName] = entry;
        return isNew;
    }

    /// <summary>
    /// Attempts to retrieve a database schema from the collection.
    /// </summary>
    public bool TryGetSchema(string databaseName, out SqlDatabaseSchema? schema)
    {
        if (_schemas.TryGetValue(databaseName, out var entry))
        {
            schema = entry.Schema;
            return true;
        }

        schema = null;
        return false;
    }

    /// <summary>
    /// Retrieves a database schema from the collection.
    /// </summary>
    public SqlDatabaseSchema GetSchema(string databaseName)
    {
        if (!_schemas.TryGetValue(databaseName, out var entry))
        {
            throw new KeyNotFoundException($"No schema found for database '{databaseName}'.");
        }

        return entry.Schema;
    }

    public bool ContainsSchema(string databaseName) => _schemas.ContainsKey(databaseName);

    public DateTime? GetRetrievedAtUtc(string databaseName)
    {
        return _schemas.TryGetValue(databaseName, out var entry)
            ? entry.RetrievedAtUtc
            : null;
    }

    public bool RemoveSchema(string databaseName) => _schemas.TryRemove(databaseName, out _);

    public void Clear() => _schemas.Clear();

    public IReadOnlyList<SqlDatabaseSchema> GetAllSchemas() => _schemas.Values.Select(e => e.Schema).ToList();

    private sealed class DatabaseSchemaEntry
    {
        public required SqlDatabaseSchema Schema { get; init; }
        public DateTime RetrievedAtUtc { get; init; }
    }
}
