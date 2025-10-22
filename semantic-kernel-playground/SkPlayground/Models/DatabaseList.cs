namespace SkPlayground.Models;

/// <summary>
/// Represents a cached list of SQL Server database names retrieved from the instance.
/// This class is used to store database information in the state manager to avoid
/// redundant queries to the SQL Server.
/// </summary>
/// <remarks>
/// The database list is typically populated by the `GetSqlDatabaseListFunction` and
/// stored in the conversation state using the key "database_list".
/// </remarks>
public sealed class DatabaseList
{
    /// <summary>
    /// Gets or sets the collection of database names available on the SQL Server instance.
    /// </summary>
    /// <value>
    /// A read-only list containing the names of all databases on the server.
    /// Returns an empty list if no databases are present.
    /// </value>
    public IReadOnlyList<string> Databases { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the timestamp when this database list was retrieved.
    /// </summary>
    /// <value>
    /// The UTC DateTime when the database list was queried from the SQL Server.
    /// Useful for cache invalidation and determining if the list should be refreshed.
    /// </value>
    public DateTime RetrievedAtUtc { get; set; } = DateTime.UtcNow;
}
