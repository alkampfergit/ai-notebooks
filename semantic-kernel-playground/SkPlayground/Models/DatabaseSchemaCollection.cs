using System.Collections.Concurrent;

namespace SkPlayground.Models;

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
///
/// **Usage Example:**
/// ```csharp
/// // Retrieve the collection from state
/// var collection = StateManager.GetMemoryValue&lt;DatabaseSchemaCollection&gt;("database_schema_collection");
///
/// // Add a new schema
/// collection.AddSchema(schema);
///
/// // Get a specific schema
/// if (collection.TryGetSchema("MyDatabase", out var schema))
/// {
///     // Use the schema...
/// }
/// ```
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
    /// <value>
    /// A collection of database names (case-insensitive) that can be used to enumerate
    /// all databases with cached schemas.
    /// </value>
    public IReadOnlyCollection<string> DatabaseNames => _schemas.Keys.ToList();

    /// <summary>
    /// Adds or updates a database schema in the collection.
    /// </summary>
    /// <param name="schema">The SQL database schema to add or update.</param>
    /// <returns>
    /// Returns `true` if this is a new schema being added, `false` if an existing schema
    /// for the same database is being updated.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="schema"/> is null.</exception>
    /// <remarks>
    /// If a schema for the specified database already exists, it will be replaced with the new schema.
    /// The `RetrievedAtUtc` timestamp is automatically set to the current UTC time.
    /// </remarks>
    public bool AddSchema(SqlDatabaseSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var entry = new DatabaseSchemaEntry
        {
            Schema = schema,
            RetrievedAtUtc = DateTime.UtcNow
        };

        // AddOrUpdate returns the new value, but we need to know if it was added or updated
        bool isNew = !_schemas.ContainsKey(schema.DatabaseName);
        _schemas[schema.DatabaseName] = entry;
        return isNew;
    }

    /// <summary>
    /// Attempts to retrieve a database schema from the collection.
    /// </summary>
    /// <param name="databaseName">The name of the database (case-insensitive).</param>
    /// <param name="schema">
    /// When this method returns, contains the database schema if found; otherwise, null.
    /// </param>
    /// <returns>
    /// `true` if the schema was found in the collection; otherwise, `false`.
    /// </returns>
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
    /// <param name="databaseName">The name of the database (case-insensitive).</param>
    /// <returns>The SQL database schema if found.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no schema exists for the specified database name.
    /// </exception>
    public SqlDatabaseSchema GetSchema(string databaseName)
    {
        if (!_schemas.TryGetValue(databaseName, out var entry))
        {
            throw new KeyNotFoundException($"No schema found for database '{databaseName}'.");
        }

        return entry.Schema;
    }

    /// <summary>
    /// Checks if a schema exists for the specified database.
    /// </summary>
    /// <param name="databaseName">The name of the database (case-insensitive).</param>
    /// <returns>`true` if a schema exists; otherwise, `false`.</returns>
    public bool ContainsSchema(string databaseName)
    {
        return _schemas.ContainsKey(databaseName);
    }

    /// <summary>
    /// Gets the timestamp when the schema for a specific database was retrieved.
    /// </summary>
    /// <param name="databaseName">The name of the database (case-insensitive).</param>
    /// <returns>
    /// The UTC DateTime when the schema was retrieved, or null if the database schema is not found.
    /// </returns>
    public DateTime? GetRetrievedAtUtc(string databaseName)
    {
        return _schemas.TryGetValue(databaseName, out var entry)
            ? entry.RetrievedAtUtc
            : null;
    }

    /// <summary>
    /// Removes a database schema from the collection.
    /// </summary>
    /// <param name="databaseName">The name of the database to remove (case-insensitive).</param>
    /// <returns>`true` if the schema was removed; `false` if it was not found.</returns>
    public bool RemoveSchema(string databaseName)
    {
        return _schemas.TryRemove(databaseName, out _);
    }

    /// <summary>
    /// Clears all database schemas from the collection.
    /// </summary>
    public void Clear()
    {
        _schemas.Clear();
    }

    /// <summary>
    /// Gets all schemas currently stored in the collection.
    /// </summary>
    /// <returns>A read-only list of all database schemas.</returns>
    public IReadOnlyList<SqlDatabaseSchema> GetAllSchemas()
    {
        return _schemas.Values.Select(e => e.Schema).ToList();
    }

    /// <summary>
    /// Internal wrapper class that stores a database schema along with metadata.
    /// </summary>
    private sealed class DatabaseSchemaEntry
    {
        /// <summary>
        /// The SQL database schema containing table and column information.
        /// </summary>
        public required SqlDatabaseSchema Schema { get; init; }

        /// <summary>
        /// The UTC timestamp when this schema was retrieved from the SQL Server.
        /// </summary>
        public DateTime RetrievedAtUtc { get; init; }
    }
}
