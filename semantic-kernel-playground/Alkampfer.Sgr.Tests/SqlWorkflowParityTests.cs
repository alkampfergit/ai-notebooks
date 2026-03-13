using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Alkampfer.Sgr.Models;
using Alkampfer.Sgr.Services;
using Alkampfer.Sgr.Playground.BusinessFunctions;
using Alkampfer.Sgr.Playground.Services;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Alkampfer.Sgr.Tests;

/// <summary>
/// Verifies that the refactored SQL workflow composition still behaves like the
/// original playground wiring, without depending on a live database or model.
/// </summary>
[TestFixture]
public class SqlWorkflowParityTests
{
    private static readonly Type[] SqlWorkflowToolTypes =
    [
        typeof(ReportTaskCompletionToolCall),
        typeof(GetDatabaseNamesFromServerToolCall),
        typeof(GetSqlDatabaseSchemaToolCall),
        typeof(ExecuteSqlQueryToolCall),
        typeof(ExportSqlQueryResultToolCall)
    ];

    [SetUp]
    public void SetUp()
    {
        StateManager.Clear();
        StateManager.Start();
    }

    [TearDown]
    public void TearDown()
    {
        StateManager.Clear();
    }

    /// <summary>
    /// Confirms that the specialized SQL workflow factory exposes the same tool
    /// set composed in Program.cs and does not leak unrelated business tools.
    /// </summary>
    [Test]
    public void SqlWorkflowFactory_GenerateSchema_ShouldExposeOnlySqlWorkflowTools()
    {
        var factory = CreateSqlWorkflowFactory();

        var result = factory.GenerateSchemaWithDocumentationForToolCall();
        var definitions = JsonNode.Parse(result.JsonSchema)?["definitions"]?.AsObject();

        Assert.That(definitions, Is.Not.Null, "The generated SQL workflow schema should contain tool definitions.");
        Assert.That(definitions!.ContainsKey(nameof(ReportTaskCompletionToolCall)), Is.True);
        Assert.That(definitions.ContainsKey(nameof(GetDatabaseNamesFromServerToolCall)), Is.True);
        Assert.That(definitions.ContainsKey(nameof(GetSqlDatabaseSchemaToolCall)), Is.True);
        Assert.That(definitions.ContainsKey(nameof(ExecuteSqlQueryToolCall)), Is.True);

        // Export is state-dependent and should remain hidden until a query result exists.
        Assert.That(definitions.ContainsKey(nameof(ExportSqlQueryResultToolCall)), Is.False);

        // The SQL workflow reasoner should stay isolated from the customer/invoice tools.
        Assert.That(definitions.ContainsKey(nameof(SendEmailToolCall)), Is.False);
        Assert.That(definitions.ContainsKey(nameof(IssueInvoiceToolCall)), Is.False);
        Assert.That(definitions.ContainsKey(nameof(GetCustomerDataToolCall)), Is.False);
        Assert.That(definitions.ContainsKey(nameof(VoidInvoiceToolCall)), Is.False);
        Assert.That(definitions.ContainsKey(nameof(CreateRuleToolCall)), Is.False);
    }

    /// <summary>
    /// Confirms that the refactored SQL workflow still honors the state-driven
    /// tool availability used by the original playground scenario.
    /// </summary>
    [Test]
    public void SqlWorkflowFactory_Availability_ShouldTrackStateDrivenSqlTools()
    {
        var factory = CreateSqlWorkflowFactory();

        var initialDefinitions = JsonNode.Parse(factory.GenerateSchemaWithDocumentationForToolCall().JsonSchema)?["definitions"]?.AsObject();

        Assert.That(initialDefinitions, Is.Not.Null);
        Assert.That(initialDefinitions!.ContainsKey(nameof(GetDatabaseNamesFromServerToolCall)), Is.True,
            "Database discovery should be available before the database list is cached.");
        Assert.That(initialDefinitions.ContainsKey(nameof(ExportSqlQueryResultToolCall)), Is.False,
            "Export should be unavailable before any query result is cached.");

        StateManager.SetMemoryValue("database_list", new DatabaseList { Databases = ["northwind"] });

        var resultCollection = new SqlQueryResultCollection();
        resultCollection.AddResult(new SqlQueryExecutionResult(
            "orders_2026",
            "northwind",
            "SELECT 1",
            1,
            [new SqlQueryResultTable("Orders", ["Value"], 1, [new Dictionary<string, object?> { ["Value"] = 1 }])],
            DateTime.UtcNow));
        StateManager.SetMemoryValue("sql_query_result_collection", resultCollection);

        var updatedDefinitions = JsonNode.Parse(factory.GenerateSchemaWithDocumentationForToolCall().JsonSchema)?["definitions"]?.AsObject();

        Assert.That(updatedDefinitions, Is.Not.Null);
        Assert.That(updatedDefinitions!.ContainsKey(nameof(GetDatabaseNamesFromServerToolCall)), Is.False,
            "Database discovery should disappear once the list has already been cached.");
        Assert.That(updatedDefinitions.ContainsKey(nameof(ExportSqlQueryResultToolCall)), Is.True,
            "Export should become available once a query result exists in state.");
    }

    /// <summary>
    /// Confirms that the SQL workflow factory still deserializes SQL-specific
    /// NextStep payloads after the namespace split.
    /// </summary>
    [Test]
    public void SqlWorkflowFactory_DeserializeNextStep_ShouldHandleSqlToolCalls()
    {
        var factory = CreateSqlWorkflowFactory();

        var json = """
        {
          "CurrentState": "Ready to execute SQL query",
          "PlanRemainingStepsBrief": [
            "Execute the generated SQL query",
            "Report the final result"
          ],
          "TaskCompleted": false,
          "Function": {
                        "type": "execute_sql_query_tool_call",
            "DatabaseName": "northwind",
            "QueryDescription": "list the most recent orders",
            "ResultId": "recent_orders"
          }
        }
        """;

        var nextStep = factory.DeserializeNextStep(json);

        Assert.That(nextStep, Is.Not.Null);
        Assert.That(nextStep!.Function, Is.TypeOf<ExecuteSqlQueryToolCall>());

        var toolCall = (ExecuteSqlQueryToolCall)nextStep.Function;
        Assert.That(toolCall.DatabaseName, Is.EqualTo("northwind"));
        Assert.That(toolCall.QueryDescription, Is.EqualTo("list the most recent orders"));
        Assert.That(toolCall.ResultId, Is.EqualTo("recent_orders"));
    }

    /// <summary>
    /// Exercises the main schema-guided reasoner end-to-end with a fake chat
    /// service so the refactored factory and reasoner wiring are validated in
    /// one round-trip without calling external services.
    /// </summary>
    [Test]
    public async Task SchemaGuidedReasoner_WithStubCompletion_ShouldReturnCompletionSummary()
    {
        var assistantJson = """
        {
          "CurrentState": "SQL workflow completed",
          "PlanRemainingStepsBrief": [
            "Report completion to the user"
          ],
          "TaskCompleted": true,
          "Function": {
            "type": "ReportTaskCompletion",
            "Summary": "SQL workflow completed successfully."
          }
        }
        """;

        var kernel = CreateKernel(new StubChatCompletionService(assistantJson));

        var factory = CreateSqlWorkflowFactory(kernel, new[] { typeof(ReportTaskCompletionToolCall) });
        var reasoner = new SchemaGuidedReasoner(kernel, factory)
        {
            VerboseOutput = false
        };

        var result = await reasoner.ReasonAndActAsync("Summarize the SQL workflow.");

        Assert.That(result, Is.EqualTo("SQL workflow completed successfully."));
    }

    private static BusinessFunctionFactory CreateSqlWorkflowFactory(Kernel? kernel = null, Type[]? toolTypes = null)
    {
        kernel ??= CreateKernel();
        var loggerFactory = kernel.Services.GetRequiredService<ILoggerFactory>();

        return new BusinessFunctionFactory(
            new DatabaseService(),
            new SqlServerService(),
            kernel,
            loggerFactory,
            toolTypes ?? SqlWorkflowToolTypes);
    }

    private static Kernel CreateKernel(IChatCompletionService? chatCompletionService = null)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        if (chatCompletionService != null)
        {
            kernelBuilder.Services.AddSingleton<IChatCompletionService>(chatCompletionService);
        }

        return kernelBuilder.Build();
    }

    /// <summary>
    /// Minimal deterministic chat completion service used to test the reasoner
    /// without network calls.
    /// </summary>
    private sealed class StubChatCompletionService : IChatCompletionService
    {
        private readonly string _assistantMessage;

        public StubChatCompletionService(string assistantMessage)
        {
            _assistantMessage = assistantMessage;
        }

        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            var openAiMessage = CreateOpenAiMessage(_assistantMessage);
            IReadOnlyList<ChatMessageContent> messages =
            [
                openAiMessage
            ];

            return Task.FromResult(messages);
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }

        private static OpenAIChatMessageContent CreateOpenAiMessage(string content)
        {
            var constructor = typeof(OpenAIChatMessageContent)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(ctor =>
                {
                    var parameters = ctor.GetParameters();
                    return parameters.Length == 5
                        && parameters[0].ParameterType.FullName == "OpenAI.Chat.ChatMessageRole";
                });

            var openAiRole = Enum.Parse(constructor.GetParameters()[0].ParameterType, "Assistant");
            var toolCallType = constructor.GetParameters()[3].ParameterType.GetGenericArguments()[0];
            var emptyToolCalls = Array.CreateInstance(toolCallType, 0);

            return (OpenAIChatMessageContent)constructor.Invoke(
            [
                openAiRole,
                content,
                "test-model",
                emptyToolCalls,
                new Dictionary<string, object?>()
            ]);
        }
    }
}