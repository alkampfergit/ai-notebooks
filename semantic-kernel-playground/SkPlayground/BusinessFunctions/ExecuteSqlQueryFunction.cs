using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using SkPlayground.Models;
using SkPlayground.Services;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// Executes SQL queries against the configured SQL Server instance and stores the full result for later reuse.
/// </summary>
public sealed class ExecuteSqlQueryFunction : BusinessFunction<ExecuteSqlQueryToolCall>
{
    private readonly SqlServerService _sqlServerService;

    public ExecuteSqlQueryFunction(SqlServerService sqlServerService)
    {
        _sqlServerService = sqlServerService;
    }

    protected override async Task<BusinessFunctionResult> ExecuteAsync(
        ExecuteSqlQueryToolCall parameters,
        CancellationToken cancellationToken = default)
    {
        var executionResult = await _sqlServerService.ExecuteSqlQueryAsync(
            parameters.DatabaseName,
            parameters.SqlQuery,
            parameters.MaxPreviewRows,
            parameters.ResultId,
            cancellationToken).ConfigureAwait(false);

        string summary;
        if (executionResult.Tables.Count == 0)
        {
            summary = $"Executed query on {executionResult.DatabaseName}; no result sets were returned.";
        }
        else
        {
            var tableSummaries = executionResult.Tables
                .Select(t => $"{t.TableName} ({t.RowCount} rows)")
                .ToArray();

            summary = $"Executed query on {executionResult.DatabaseName}; retrieved {executionResult.TotalRows} rows across {executionResult.Tables.Count} result set(s) stored as '{executionResult.ResultId}' [{string.Join(", ", tableSummaries)}].";
        }

        return new BusinessFunctionResult(executionResult, summary);
    }
}

/// <summary>
/// Parameters required to execute a SQL query.
/// </summary>
[Description("Execute a SQL statement against a SQL Server database")]
public sealed class ExecuteSqlQueryToolCall : ToolCall
{
    [Description("Name of the database where the query should run")]
    [Required]
    public required string DatabaseName { get; set; }

    [Description("SQL text to execute. Must be valid T-SQL for SQL Server.")]
    [Required]
    public required string SqlQuery { get; set; }

    [Description("Maximum number of preview rows per result set returned in the response")]
    [Range(1, 500)]
    public int MaxPreviewRows { get; set; } = 50;

    [Description("Optional identifier to use for storing the query result. If omitted a unique id is generated.")]
    public string? ResultId { get; set; }

    public override string Type => "execute_sql_query_tool_call";
}
