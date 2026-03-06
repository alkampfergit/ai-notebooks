using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using Alkampfer.Sgr.Models;
using Alkampfer.Sgr.Playground.Utils;

namespace Alkampfer.Sgr.Playground.Services;

/// <summary>
/// Provides access to a SQL Server instance along with lightweight state
/// tracking for schema discovery, query execution, and result exports.
/// </summary>
public class SqlServerService
{
    private readonly string _connectionString;
    private readonly object _databaseListLock = new();

    private IReadOnlyList<string>? _cachedDatabaseList;
    private readonly ConcurrentDictionary<string, SqlDatabaseSchema> _schemaCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, StoredQueryResult> _queryResults = new(StringComparer.OrdinalIgnoreCase);

    private const int DefaultCommandTimeoutSeconds = 120;

    /// <summary>
    /// Creates the service using an explicit connection string or the SQLSERVER_CONNECTION_STRING environment variable.
    /// </summary>
    public SqlServerService(string? connectionString = null)
    {
        var configuredConnection = connectionString ?? Dotenv.Get("SQLSERVER_CONNECTION_STRING");
        _connectionString = string.IsNullOrWhiteSpace(configuredConnection)
            ? "Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;"
            : configuredConnection;
    }

    /// <summary>
    /// Returns a cached list of database names present in the server.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetDatabaseListAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && _cachedDatabaseList is { } cached)
        {
            return cached;
        }

        var databases = new List<string>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = "SELECT name FROM sys.databases ORDER BY name;";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            databases.Add(reader.GetString(0));
        }

        lock (_databaseListLock)
        {
            _cachedDatabaseList = databases;
        }

        return databases;
    }

    /// <summary>
    /// Retrieves the schema for the specified database. Results are cached unless forceRefresh is true.
    /// </summary>
    public async Task<SqlDatabaseSchema> GetDatabaseSchemaAsync(
        string databaseName,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException("Database name is required", nameof(databaseName));
        }

        if (!forceRefresh && _schemaCache.TryGetValue(databaseName, out var cachedSchema))
        {
            return cachedSchema;
        }

        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = databaseName
        };

        var columnLookup = new Dictionary<(string Schema, string Table), List<SqlColumnSchema>>(SchemaTableKeyComparer.Instance);

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string schemaQuery = """
            SELECT
                TABLE_SCHEMA,
                TABLE_NAME,
                COLUMN_NAME,
                DATA_TYPE,
                IS_NULLABLE,
                ORDINAL_POSITION
            FROM INFORMATION_SCHEMA.COLUMNS
            ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;
            """;

        await using var command = new SqlCommand(schemaQuery, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = (Schema: reader.GetString(0), Table: reader.GetString(1));
            if (!columnLookup.TryGetValue(key, out var columns))
            {
                columns = new List<SqlColumnSchema>();
                columnLookup[key] = columns;
            }

            var column = new SqlColumnSchema(
                reader.GetString(2),
                reader.GetString(3),
                string.Equals(reader.GetString(4), "YES", StringComparison.OrdinalIgnoreCase),
                reader.GetInt32(5));

            columns.Add(column);
        }

        var tables = columnLookup
            .OrderBy(kvp => $"{kvp.Key.Schema}.{kvp.Key.Table}", StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new SqlTableSchema(
                kvp.Key.Schema,
                kvp.Key.Table,
                kvp.Value.OrderBy(c => c.OrdinalPosition).ToList()))
            .ToList();

        var schema = new SqlDatabaseSchema(databaseName, tables);
        _schemaCache[databaseName] = schema;
        return schema;
    }

    /// <summary>
    /// Executes a SQL query and stores the full result for later reuse (e.g. Excel export).
    /// </summary>
    public async Task<SqlQueryExecutionResult> ExecuteSqlQueryAsync(
        string databaseName,
        string sqlQuery,
        int maxPreviewRows = 50,
        string? preferredResultId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException("Database name is required", nameof(databaseName));
        }

        if (string.IsNullOrWhiteSpace(sqlQuery))
        {
            throw new ArgumentException("SQL query must not be empty.", nameof(sqlQuery));
        }

        var resultId = GenerateResultId(preferredResultId);

        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = databaseName
        };

        var dataSet = new DataSet("QueryResult");
        var tables = new List<SqlQueryResultTable>();
        var totalRows = 0;

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new SqlCommand(sqlQuery, connection)
        {
            CommandTimeout = DefaultCommandTimeoutSeconds
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var resultIndex = 0;
        do
        {
            if (reader.FieldCount == 0)
            {
                continue;
            }

            var schemaTable = reader.GetSchemaTable();
            var tableName = GetTableName(schemaTable, resultIndex);

            var dataTable = new DataTable(tableName);
            var columns = new List<string>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    columnName = $"Column_{i + 1}";
                }

                columns.Add(columnName);
                dataTable.Columns.Add(columnName, typeof(object));
            }

            var previewRows = new List<IDictionary<string, object?>>();
            var rowValues = new object[reader.FieldCount];
            var rowCount = 0;

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                reader.GetValues(rowValues);
                totalRows++;
                rowCount++;

                var newRow = new object[rowValues.Length];
                Array.Copy(rowValues, newRow, rowValues.Length);
                dataTable.Rows.Add(newRow);

                if (previewRows.Count < maxPreviewRows)
                {
                    var rowDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < columns.Count; i++)
                    {
                        rowDict[columns[i]] = rowValues[i];
                    }
                    previewRows.Add(rowDict);
                }
            }

            dataSet.Tables.Add(dataTable);

            tables.Add(new SqlQueryResultTable(
                tableName,
                columns,
                rowCount,
                previewRows));

            resultIndex++;
        }
        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));

        var executionResult = new SqlQueryExecutionResult(
            resultId,
            databaseName,
            sqlQuery,
            totalRows,
            tables,
            DateTime.UtcNow);

        _queryResults[resultId] = new StoredQueryResult(executionResult, dataSet);
        return executionResult;
    }

    /// <summary>
    /// Exports a previously stored query result to Excel using EPPlus.
    /// </summary>
    public async Task<SqlExportResult> ExportQueryResultToExcelAsync(
        string resultId,
        string? targetPath = null,
        CancellationToken cancellationToken = default)
    {
        if (!_queryResults.TryGetValue(resultId, out var storedResult))
        {
            throw new InvalidOperationException($"No query result found for id '{resultId}'.");
        }

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var destinationPath = ResolveOutputPath(resultId, targetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        using var package = new ExcelPackage();

        var worksheets = new List<string>();

        foreach (DataTable table in storedResult.Data.Tables)
        {
            var worksheetName = GetSafeWorksheetName($"{resultId}_{table.TableName}", worksheets.Count + 1);
            worksheets.Add(worksheetName);
            var worksheet = package.Workbook.Worksheets.Add(worksheetName);
            worksheet.Cells["A1"].LoadFromDataTable(table, true);
            worksheet.Cells.AutoFitColumns();
        }

        await package.SaveAsAsync(new FileInfo(destinationPath), cancellationToken).ConfigureAwait(false);

        return new SqlExportResult(resultId, destinationPath, worksheets);
    }

    /// <summary>
    /// Retrieves the cached execution result if available.
    /// </summary>
    public bool TryGetExecutedQuery(string resultId, out SqlQueryExecutionResult? result)
    {
        if (_queryResults.TryGetValue(resultId, out var storedResult))
        {
            result = storedResult.ExecutionSummary;
            return true;
        }

        result = null;
        return false;
    }

    private string GenerateResultId(string? preferredId)
    {
        var baseId = string.IsNullOrWhiteSpace(preferredId)
            ? $"result_{DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}"
            : SanitizeIdentifier(preferredId);

        var candidate = baseId;
        var counter = 2;

        while (_queryResults.ContainsKey(candidate))
        {
            candidate = $"{baseId}_{counter++}";
        }

        return candidate;
    }

    private static string SanitizeIdentifier(string input)
    {
        var builder = new StringBuilder();
        foreach (var ch in input)
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_' )
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (char.IsWhiteSpace(ch))
            {
                builder.Append('_');
            }
        }

        if (builder.Length == 0)
        {
            builder.Append("result");
        }

        return builder.ToString();
    }

    private static string GetTableName(DataTable? schemaTable, int resultIndex)
    {
        if (schemaTable == null || schemaTable.Rows.Count == 0)
        {
            return $"ResultSet_{resultIndex + 1}";
        }

        var column = schemaTable.Rows[0];
        var tableName = column["BaseTableName"] as string;

        if (string.IsNullOrWhiteSpace(tableName))
        {
            tableName = $"ResultSet_{resultIndex + 1}";
        }

        return tableName;
    }

    private static string ResolveOutputPath(string resultId, string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return Path.Combine(Path.GetTempPath(), $"{resultId}.xlsx");
        }

        if (Directory.Exists(targetPath))
        {
            return Path.Combine(targetPath, $"{resultId}.xlsx");
        }

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return Path.GetExtension(targetPath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? targetPath
            : $"{targetPath}.xlsx";
    }

    private static string GetSafeWorksheetName(string candidate, int fallbackIndex)
    {
        var sanitized = new string(candidate
            .Select(ch => invalidWorksheetChars.Contains(ch) ? '_' : ch)
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = $"Sheet{fallbackIndex}";
        }

        return sanitized.Length > 31
            ? sanitized[..31]
            : sanitized;
    }

    private static readonly HashSet<char> invalidWorksheetChars =
    [
        '\\', '/', '*', '[', ']', ':', '?'
    ];

    private sealed class SchemaTableKeyComparer : IEqualityComparer<(string Schema, string Table)>
    {
        public static readonly SchemaTableKeyComparer Instance = new();

        public bool Equals((string Schema, string Table) x, (string Schema, string Table) y) =>
            string.Equals(x.Schema, y.Schema, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Table, y.Table, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Schema, string Table) obj)
        {
            var hash = new HashCode();
            hash.Add(obj.Schema ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.Table ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }

    private record StoredQueryResult(SqlQueryExecutionResult ExecutionSummary, DataSet Data);
}
