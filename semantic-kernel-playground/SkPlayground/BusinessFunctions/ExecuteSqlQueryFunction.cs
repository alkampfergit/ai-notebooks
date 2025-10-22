#pragma warning disable OPENAI001

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SkPlayground.Models;
using SkPlayground.Services;

namespace SkPlayground.BusinessFunctions;

/// <summary>
/// Executes SQL queries against the configured SQL Server instance and stores the full result for later reuse.
/// </summary>
/// <remarks>
/// This function supports two modes of operation:
///
/// **1. Direct T-SQL Execution:**
/// When `SqlQuery` is provided, the query is executed directly against the database.
///
/// **2. Natural Language to T-SQL Translation:**
/// When `QueryDescription` is provided instead of `SqlQuery`, the function uses the Semantic Kernel
/// to translate the natural language description into a T-SQL query. The translation process:
/// - Retrieves the database schema from the state manager (if available)
/// - Uses the schema as context for the LLM to generate accurate queries
/// - Validates that the generated query is valid T-SQL
/// - Executes the translated query
///
/// **State Manager Integration:**
/// The function checks for a `DatabaseSchemaCollection` in state manager under the key
/// "database_schema_collection". If the schema for the target database is found, it's included
/// in the LLM prompt to improve query generation accuracy.
/// </remarks>
public sealed class ExecuteSqlQueryFunction : BusinessFunction<ExecuteSqlQueryToolCall>
{
    private readonly SqlServerService _sqlServerService;
    private readonly Kernel _kernel;
    private readonly ILogger<ExecuteSqlQueryFunction> _logger;

    /// <summary>
    /// State manager key for retrieving the database schema collection.
    /// </summary>
    private const string SchemaCollectionStateKey = "database_schema_collection";

    public ExecuteSqlQueryFunction(
        SqlServerService sqlServerService,
        Kernel kernel,
        ILogger<ExecuteSqlQueryFunction> logger)
    {
        _sqlServerService = sqlServerService;
        _kernel = kernel;
        _logger = logger;
    }

    protected override async Task<BusinessFunctionResult> ExecuteAsync(
        ExecuteSqlQueryToolCall parameters,
        CancellationToken cancellationToken = default)
    {
        // Determine the final SQL query to execute
        string sqlQueryToExecute;

        if (!string.IsNullOrWhiteSpace(parameters.SqlQuery))
        {
            // Mode 1: Direct T-SQL execution
            sqlQueryToExecute = parameters.SqlQuery;
            _logger.LogInformation(
                "Executing direct T-SQL query on database '{DatabaseName}'",
                parameters.DatabaseName);
        }
        else if (!string.IsNullOrWhiteSpace(parameters.QueryDescription))
        {
            // Mode 2: Natural language to T-SQL translation
            _logger.LogInformation(
                "Translating natural language query to T-SQL for database '{DatabaseName}'",
                parameters.DatabaseName);

            sqlQueryToExecute = await TranslateNaturalLanguageToSqlAsync(
                parameters.DatabaseName,
                parameters.QueryDescription,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Generated T-SQL query: {SqlQuery}",
                sqlQueryToExecute);
        }
        else
        {
            throw new InvalidOperationException(
                "Either SqlQuery or QueryDescription must be provided.");
        }

        // Execute the query
        var executionResult = await _sqlServerService.ExecuteSqlQueryAsync(
            parameters.DatabaseName,
            sqlQueryToExecute,
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

    /// <summary>
    /// Translates a natural language query description into a T-SQL query using the Semantic Kernel.
    /// </summary>
    /// <param name="databaseName">The target database name.</param>
    /// <param name="queryDescription">Natural language description of the desired query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A valid T-SQL query string.</returns>
    /// <remarks>
    /// This method constructs a prompt for the LLM that includes:
    /// - The database schema (if available in state manager)
    /// - SQL Server T-SQL syntax guidelines
    /// - The user's natural language query description
    ///
    /// The LLM is instructed to return ONLY the T-SQL query without any markdown formatting
    /// or additional explanation.
    /// </remarks>
    private async Task<string> TranslateNaturalLanguageToSqlAsync(
        string databaseName,
        string queryDescription,
        CancellationToken cancellationToken)
    {
        // Check if schema is available in state manager
        string schemaContext = string.Empty;
        if (StateManager.TryGetMemoryValue<DatabaseSchemaCollection>(
            SchemaCollectionStateKey,
            out var schemaCollection) && schemaCollection != null)
        {
            if (schemaCollection.TryGetSchema(databaseName, out var schema))
            {
                _logger.LogInformation(
                    "Found schema for database '{DatabaseName}' in state manager with {TableCount} tables",
                    databaseName,
                    schema.Tables.Count);

                schemaContext = $@"

## Database Schema for '{databaseName}'

{schema.ToPromptString()}
";
            }
            else
            {
                _logger.LogWarning(
                    "Schema for database '{DatabaseName}' not found in state manager. " +
                    "Query generation may be less accurate.",
                    databaseName);
            }
        }
        else
        {
            _logger.LogWarning(
                "No schema collection found in state manager. " +
                "Query generation may be less accurate.");
        }

        // Build the system prompt for SQL generation
        var systemPrompt = $@"You are an expert SQL Server T-SQL query generator.

Your task is to convert natural language query descriptions into valid T-SQL queries.
{schemaContext}

## Important Guidelines:

1. **Output Format**: Return ONLY the T-SQL query text. Do NOT include:
   - Markdown code blocks (no ```sql or ```)
   - Explanations or comments outside the query
   - Any additional text before or after the query

2. **T-SQL Syntax**: Use proper SQL Server T-SQL syntax:
   - Use square brackets [TableName] for identifiers with spaces or reserved words
   - Use TOP instead of LIMIT
   - Use GETDATE() for current date/time
   - Use appropriate SQL Server data types and functions

3. **Schema Usage**: Use the provided schema information to:
   - Reference correct table and column names
   - Use appropriate data types in conditions
   - Respect nullable columns

4. **Query Quality**:
   - Write efficient, well-structured queries
   - Use appropriate JOINs when needed
   - Include ORDER BY when results should be sorted
   - Add WHERE clauses for filtering when appropriate

5. **Safety**:
   - Generate SELECT statements only (no INSERT, UPDATE, DELETE, DROP, etc.)
   - Do not use dynamic SQL or EXEC statements
   - Avoid potentially dangerous operations";

        var userMessage = $@"Database: {databaseName}

Query Description: {queryDescription}

Generate the T-SQL query:";

        // Use the chat completion service to generate the query
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(userMessage);

        var response = await chatService.GetChatMessageContentAsync(
            chatHistory,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var generatedSql = response.Content?.Trim() ?? string.Empty;

        // Clean up any markdown code blocks that might have been included
        generatedSql = CleanSqlQuery(generatedSql);

        if (string.IsNullOrWhiteSpace(generatedSql))
        {
            throw new InvalidOperationException(
                "Failed to generate SQL query from natural language description.");
        }

        return generatedSql;
    }

    /// <summary>
    /// Cleans up the generated SQL query by removing markdown formatting and extra whitespace.
    /// </summary>
    /// <param name="sql">The raw SQL query text from the LLM.</param>
    /// <returns>Clean T-SQL query ready for execution.</returns>
    private static string CleanSqlQuery(string sql)
    {
        // Remove markdown code blocks
        sql = sql.Replace("```sql", "").Replace("```tsql", "").Replace("```", "");

        // Trim whitespace
        sql = sql.Trim();

        return sql;
    }
}

/// <summary>
/// Parameters required to execute a SQL query.
/// </summary>
/// <remarks>
/// This tool call supports two modes of operation:
///
/// **Mode 1: Direct T-SQL Execution** (provide `SqlQuery`)
/// - Executes the provided T-SQL query directly against the database
/// - Useful when you already have a valid SQL query
///
/// **Mode 2: Natural Language Query** (provide `QueryDescription`)
/// - Translates a natural language description into T-SQL
/// - Uses the Semantic Kernel LLM to generate the query
/// - Automatically includes database schema context if available
/// - Useful for generating queries from user requests
///
/// **Note:** Either `SqlQuery` OR `QueryDescription` must be provided, but not both.
/// </remarks>
[Description("Execute a SQL statement against a SQL Server database, either directly or by translating a natural language description")]
public sealed class ExecuteSqlQueryToolCall : ToolCall
{
    [Description("Name of the database where the query should run")]
    [Required]
    public required string DatabaseName { get; set; }

    [Description("SQL text to execute. Must be valid T-SQL for SQL Server. Either SqlQuery or QueryDescription must be provided.")]
    public string? SqlQuery { get; set; }

    [Description("Natural language description of the query to execute. The system will translate this to T-SQL using the database schema. Either SqlQuery or QueryDescription must be provided.")]
    public string? QueryDescription { get; set; }

    [Description("Maximum number of preview rows per result set returned in the response")]
    [Range(1, 500)]
    public int MaxPreviewRows { get; set; } = 50;

    [Description("Optional identifier to use for storing the query result. If omitted a unique id is generated.")]
    public string? ResultId { get; set; }

    public override string Type => "ExecuteSqlQueryToolCall";
}
