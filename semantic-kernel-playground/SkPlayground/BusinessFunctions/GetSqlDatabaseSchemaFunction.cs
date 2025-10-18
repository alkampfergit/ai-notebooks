using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using SkPlayground.Models;
using SkPlayground.Services;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// Business function that retrieves table and column information for a specific SQL Server database.
/// </summary>
public sealed class GetSqlDatabaseSchemaFunction : BusinessFunction<GetSqlDatabaseSchemaToolCall>
{
    private readonly SqlServerService _sqlServerService;

    public GetSqlDatabaseSchemaFunction(SqlServerService sqlServerService)
    {
        _sqlServerService = sqlServerService;
    }

    protected override async Task<BusinessFunctionResult> ExecuteAsync(
        GetSqlDatabaseSchemaToolCall parameters,
        CancellationToken cancellationToken = default)
    {
        var schema = await _sqlServerService
            .GetDatabaseSchemaAsync(parameters.DatabaseName, parameters.RefreshCache, cancellationToken)
            .ConfigureAwait(false);

        var summary = schema.Tables.Count == 0
            ? $"Database {parameters.DatabaseName} does not contain any tables."
            : $"Retrieved schema for {parameters.DatabaseName} with {schema.Tables.Count} tables.";

        return new BusinessFunctionResult(schema, summary);
    }
}

/// <summary>
/// Parameters required to obtain a database schema.
/// </summary>
[Description("Retrieve tables and columns for a SQL Server database")]
public sealed class GetSqlDatabaseSchemaToolCall : ToolCall
{
    [Description("Name of the database to inspect")]
    [Required]
    public required string DatabaseName { get; set; }

    [Description("When true, forces refreshing the schema cache")]
    public bool RefreshCache { get; set; }

    public override string Type => "get_sql_database_schema_tool_call";
}
