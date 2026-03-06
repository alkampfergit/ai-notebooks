using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Alkampfer.Sgr.Models;
using Alkampfer.Sgr.BusinessFunctions;
using Alkampfer.Sgr.Services;
using Alkampfer.Sgr.Playground.Services;

namespace Alkampfer.Sgr.Playground.BusinessFunctions;

/// <summary>
/// Business function that retrieves table and column information for a specific SQL Server database.
/// </summary>
/// <remarks>
/// This function retrieves the database schema (tables and columns) from the SQL Server and stores it
/// in a centralized `DatabaseSchemaCollection` within the state manager. The collection is stored using
/// the key "database_schema_collection" and maintains schemas for all databases queried during the
/// conversation.
///
/// **State Management:**
/// - If no schema collection exists in state, a new one is created automatically.
/// - If a schema for the requested database already exists, a warning is logged before updating it.
/// - All schemas are stored in a single collection object to minimize state manager entries.
/// </remarks>
public sealed class GetSqlDatabaseSchemaFunction : BusinessFunction<GetSqlDatabaseSchemaToolCall>
{
    private readonly SqlServerService _sqlServerService;
    private readonly ILogger<GetSqlDatabaseSchemaFunction> _logger;

    /// <summary>
    /// State manager key used to store and retrieve the database schema collection in conversation state.
    /// </summary>
    private const string SchemaCollectionStateKey = "database_schema_collection";

    public GetSqlDatabaseSchemaFunction(
        SqlServerService sqlServerService,
        ILogger<GetSqlDatabaseSchemaFunction> logger)
    {
        _sqlServerService = sqlServerService;
        _logger = logger;
    }

    protected override async Task<BusinessFunctionResult> ExecuteAsync(
        GetSqlDatabaseSchemaToolCall parameters,
        CancellationToken cancellationToken = default)
    {
        // Retrieve or create the schema collection from state manager
        DatabaseSchemaCollection schemaCollection;
        if (StateManager.TryGetMemoryValue<DatabaseSchemaCollection>(
            SchemaCollectionStateKey,
            out var existingCollection) && existingCollection != null)
        {
            schemaCollection = existingCollection;

            // Check if schema for this specific database already exists
            if (schemaCollection.ContainsSchema(parameters.DatabaseName))
            {
                _logger.LogWarning(
                    "Schema for database '{DatabaseName}' already exists in state manager. " +
                    "It will be overwritten with fresh data.",
                    parameters.DatabaseName);
            }
        }
        else
        {
            // Create a new collection if none exists
            schemaCollection = new DatabaseSchemaCollection();
        }

        // Retrieve the database schema from SQL Server
        var schema = await _sqlServerService
            .GetDatabaseSchemaAsync(parameters.DatabaseName, false, cancellationToken)
            .ConfigureAwait(false);

        // Add the schema to the collection
        schemaCollection.AddSchema(schema);

        // Store the updated collection back in the state manager
        StateManager.SetMemoryValue(SchemaCollectionStateKey, schemaCollection);

        var summary = schema.Tables.Count == 0
            ? $"Database {parameters.DatabaseName} does not contain any tables."
            : $"Retrieved schema for {parameters.DatabaseName} with {schema.Tables.Count} tables. \n " +
            $"All tables names are: {string.Join("\n", schema.Tables.Select(t => t.SchemaName + "." + t.TableName))}";

        return new BusinessFunctionResult(schema, summary);
    }
}

/// <summary>
/// Parameters required to obtain a database schema.
/// </summary>
[Description("Retrieve Schema, table names and columns for a given Database name")]
public sealed class GetSqlDatabaseSchemaToolCall : ToolCall
{
    [Description("Name of the database to inspect.")]
    [Required]
    public required string DatabaseName { get; set; }

    public override string Type => "GetSqlDatabaseSchema";
}
