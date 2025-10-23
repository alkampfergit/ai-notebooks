using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using SkPlayground.Models;
using SkPlayground.Services;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// Business function that exports a previously executed SQL query result to an Excel file.
/// </summary>
public sealed class ExportSqlQueryResultFunction : BusinessFunction<ExportSqlQueryResultToolCall>
{
    private readonly SqlServerService _sqlServerService;

    public ExportSqlQueryResultFunction(SqlServerService sqlServerService)
    {
        _sqlServerService = sqlServerService;
    }

    public override bool IsAvailable()
    {
        //is available only if we have at least one executed query result
        return StateManager.HasMemoryType<SqlQueryResultCollection>();
    }

    protected override async Task<BusinessFunctionResult> ExecuteAsync(
        ExportSqlQueryResultToolCall parameters,
        CancellationToken cancellationToken = default)
    {
        var exportResult = await _sqlServerService
            .ExportQueryResultToExcelAsync(parameters.ResultId, parameters.TargetPath, cancellationToken)
            .ConfigureAwait(false);

        var summary = $"Exported result '{exportResult.ResultId}' to {exportResult.FilePath} ({exportResult.Worksheets.Count} worksheet(s)).";
        return new BusinessFunctionResult(exportResult, summary);
    }
}

/// <summary>
/// Parameters for exporting a query result to Excel.
/// </summary>
[Description("Export an already executed query result to an Excel file")]
public sealed class ExportSqlQueryResultToolCall : ToolCall
{
    [Description("Identifier of the query result to export")]
    [Required]
    public required string ResultId { get; set; }

    [Description("Optional target file path or directory where the Excel file should be saved")]
    public string? TargetPath { get; set; }

    public override string Type => "ExportSqlQueryResult";
}
