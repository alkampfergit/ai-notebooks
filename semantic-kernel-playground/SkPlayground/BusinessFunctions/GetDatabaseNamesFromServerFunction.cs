using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using SkPlayground.Models;
using SkPlayground.Services;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// Business function that retrieves the list of databases available on the configured SQL Server instance.
/// </summary>
/// <remarks>
/// This function retrieves the database list from the SQL Server and stores it in the state manager
/// using the key "database_list" for caching purposes. If a database list is already present in the
/// state manager, a warning is logged before overwriting it.
/// </remarks>
public sealed class GetSqlDatabaseListFunction : BusinessFunction<GetSqlDatabaseListToolCall>
{
    private readonly SqlServerService _sqlServerService;
    private readonly ILogger<GetSqlDatabaseListFunction> _logger;

    /// <summary>
    /// State manager key used to store and retrieve the database list in conversation state.
    /// </summary>
    private const string DatabaseListStateKey = "database_list";

    public GetSqlDatabaseListFunction(
        SqlServerService sqlServerService,
        ILogger<GetSqlDatabaseListFunction> logger)
    {
        _sqlServerService = sqlServerService;
        _logger = logger;
    }

    protected override async Task<BusinessFunctionResult> ExecuteAsync(
        GetDatabaseNamesFromServer parameters,
        CancellationToken cancellationToken = default)
    {
        // Check if database list already exists in state manager
        if (StateManager.ContainsMemoryKey(DatabaseListStateKey))
        {
            _logger.LogWarning(
                "Database list already exists in state manager. It will be overwritten with fresh data.");
        }

        // Retrieve the database list from SQL Server
        var databaseList = await _sqlServerService
            .GetDatabaseListAsync(false, cancellationToken)
            .ConfigureAwait(false);

        // Create a typed DatabaseList object for state storage
        var databaseListModel = new DatabaseList
        {
            Databases = databaseList,
            RetrievedAtUtc = DateTime.UtcNow
        };

        // Store the database list in the state manager for future use
        StateManager.SetMemoryValue(DatabaseListStateKey, databaseListModel);

        var summary = databaseList.Count == 0
            ? "The current instance contains no database"
            : $"Server database list: {string.Join(", ", databaseList)}";

        return new BusinessFunctionResult(new
        {
            databases = databaseList,
        }, summary);
    }
}

/// <summary>
/// Parameters required to retrieve the list of SQL Server databases.
/// </summary>
[Description("Retrieve the list of database names from the SQL Server instance")]
public sealed class GetDatabaseNamesFromServer : ToolCall
{
    public override string Type => nameof(GetDatabaseNamesFromServer);
}
