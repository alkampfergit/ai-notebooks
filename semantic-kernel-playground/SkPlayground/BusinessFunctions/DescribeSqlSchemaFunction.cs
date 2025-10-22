using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using SkPlayground.Models;
using SkPlayground.Services;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// Business function that produces a focused schema summary to help the LLM reason about table structures.
/// </summary>
public sealed class DescribeSqlSchemaFunction : BusinessFunction<DescribeSqlSchemaToolCall>
{
    private readonly SqlServerService _sqlServerService;

    public DescribeSqlSchemaFunction(SqlServerService sqlServerService)
    {
        _sqlServerService = sqlServerService;
    }

    protected override async Task<BusinessFunctionResult> ExecuteAsync(
        DescribeSqlSchemaToolCall parameters,
        CancellationToken cancellationToken = default)
    {
        var schema = await _sqlServerService
            .GetDatabaseSchemaAsync(parameters.DatabaseName, false, cancellationToken)
            .ConfigureAwait(false);

        var relevantTables = FindRelevantTables(schema, parameters);

        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine($"Schema summary for database {schema.DatabaseName}:");
        if (relevantTables.Count == 0)
        {
            summaryBuilder.AppendLine("No matching tables found. Returning entire schema.");
        }
        else
        {
            summaryBuilder.AppendLine($"{relevantTables.Count} table(s) matched the request: {string.Join(", ", relevantTables.Select(t => $"{t.SchemaName}.{t.TableName}"))}");
        }

        var schemaText = parameters.IncludeFullSchema
            ? schema.ToPromptString()
            : string.Join(Environment.NewLine, relevantTables.Select(BuildTableSummary));

        var result = new
        {
            database = schema.DatabaseName,
            question = parameters.Question,
            tables = relevantTables.Select(t => new
            {
                schema = t.SchemaName,
                table = t.TableName,
                columns = t.Columns.Select(c => new
                {
                    name = c.ColumnName,
                    data_type = c.DataType,
                    nullable = c.IsNullable
                })
            }).ToList(),
            schema_text = schemaText
        };

        return new BusinessFunctionResult(result, summaryBuilder.ToString().Trim());
    }

    private static List<SqlTableSchema> FindRelevantTables(SqlDatabaseSchema schema, DescribeSqlSchemaToolCall parameters)
    {
        var normalizedTargets = parameters.TableNames
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Select(t => t.ToLowerInvariant())
            .ToHashSet();

        var tables = schema.Tables;

        var matchingTables = tables
            .Where(t => normalizedTargets.Contains(t.TableName.ToLowerInvariant())
                     || normalizedTargets.Contains($"{t.SchemaName}.{t.TableName}".ToLowerInvariant()))
            .ToList();

        if (matchingTables.Count == 0 && !string.IsNullOrWhiteSpace(parameters.Question))
        {
            var keywords = ExtractKeywords(parameters.Question);
            matchingTables = tables
                .Where(t => keywords.Any(k =>
                    t.TableName.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    t.SchemaName.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (matchingTables.Count == 0)
        {
            matchingTables = tables.ToList();
        }

        return matchingTables;
    }

    private static string BuildTableSummary(SqlTableSchema table)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Table {table.SchemaName}.{table.TableName}");
        foreach (var column in table.Columns.OrderBy(c => c.OrdinalPosition))
        {
            var nullability = column.IsNullable ? "NULL" : "NOT NULL";
            builder.AppendLine($"- {column.ColumnName} : {column.DataType} ({nullability})");
        }
        builder.AppendLine($"End schema of table {table.SchemaName}.{table.TableName}");
        return builder.ToString();
    }

    private static IReadOnlyList<string> ExtractKeywords(string question)
    {
        var separators = new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '?', '!' };
        return question
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 2)
            .Select(word => word.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
    }
}

/// <summary>
/// Parameters used to describe specific portions of the database schema.
/// </summary>
[Description("Produce a textual description of the database schema relevant to a question")]
public sealed class DescribeSqlSchemaToolCall : ToolCall
{
    [Description("Name of the database to analyze")]
    [Required]
    public required string DatabaseName { get; set; }

    [Description("Optional natural language question used to focus the schema output")]
    public string? Question { get; set; }

    [Description("Optional explicit list of tables to include in the summary")]
    public List<string> TableNames { get; set; } = [];

    [Description("When true, includes the full schema text instead of only the filtered portion")]
    public bool IncludeFullSchema { get; set; } = true;

    public override string Type => "DescribeSqlSchema";
}
