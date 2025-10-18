using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SkPlayground.Models;

/// <summary>
/// Represents the schema details for a SQL Server database.
/// </summary>
public record SqlDatabaseSchema(
    string DatabaseName,
    IReadOnlyList<SqlTableSchema> Tables)
{
    /// <summary>
    /// Creates a human-readable representation of the schema that can be used in prompts.
    /// </summary>
    public string ToPromptString()
    {
        var lines = new List<string>();

        foreach (var table in Tables.OrderBy(t => $"{t.SchemaName}.{t.TableName}", StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"Table {table.SchemaName}.{table.TableName}");
            foreach (var column in table.Columns.OrderBy(c => c.OrdinalPosition))
            {
                var nullableFlag = column.IsNullable ? "NULL" : "NOT NULL";
                lines.Add($"- {column.ColumnName} : {column.DataType} ({nullableFlag})");
            }

            lines.Add($"End schema of table {table.SchemaName}.{table.TableName}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Represents a single table schema with its columns.
/// </summary>
public record SqlTableSchema(
    string SchemaName,
    string TableName,
    IReadOnlyList<SqlColumnSchema> Columns);

/// <summary>
/// Represents a column definition inside a SQL Server table.
/// </summary>
public record SqlColumnSchema(
    string ColumnName,
    string DataType,
    bool IsNullable,
    int OrdinalPosition);

/// <summary>
/// Contains preview information for a single result set returned by a SQL query.
/// </summary>
public record SqlQueryResultTable(
    string TableName,
    IReadOnlyList<string> Columns,
    int RowCount,
    IReadOnlyList<IDictionary<string, object?>> PreviewRows);

/// <summary>
/// Summary information returned after executing a SQL query.
/// </summary>
public record SqlQueryExecutionResult(
    string ResultId,
    string DatabaseName,
    string SqlQuery,
    int TotalRows,
    IReadOnlyList<SqlQueryResultTable> Tables,
    DateTime ExecutedAtUtc);

/// <summary>
/// Result information when exporting query data to Excel.
/// </summary>
public record SqlExportResult(
    string ResultId,
    string FilePath,
    IReadOnlyList<string> Worksheets);
