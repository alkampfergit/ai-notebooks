using System.ComponentModel;
using System.Linq;
using SkPlayground.Models;
using SkPlayground.Services;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// Business function that retrieves the list of databases available on the configured SQL Server instance.
/// </summary>
public sealed class GetSqlDatabaseListFunction : BusinessFunction<GetSqlDatabaseListToolCall>
{
    private readonly SqlServerService _sqlServerService;

    public GetSqlDatabaseListFunction(SqlServerService sqlServerService)
    {
        _sqlServerService = sqlServerService;
    }

    protected override async Task<BusinessFunctionResult> ExecuteAsync(
        GetSqlDatabaseListToolCall parameters,
        CancellationToken cancellationToken = default)
    {
        var databaseList = await _sqlServerService
            .GetDatabaseListAsync(parameters.RefreshCache, cancellationToken)
            .ConfigureAwait(false);

        var summary = databaseList.Count == 0
            ? "The current instance contains no database" 
            : $"Server database list: {string.Join(", ", databaseList)}";

        return new BusinessFunctionResult(new
        {
            databases = databaseList,
            cached = !parameters.RefreshCache
        }, summary);
    }
}

/// <summary>
/// Parameters required to retrieve the list of SQL Server databases.
/// </summary>
[Description("Retrieve the list of databases from the SQL Server instance")]
public sealed class GetSqlDatabaseListToolCall : ToolCall
{
    [Description("When true, forces refreshing the cached database list")]
    public bool RefreshCache { get; set; }

    public override string Type => "get_sql_database_list_tool_call";
}
