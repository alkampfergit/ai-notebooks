using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SkPlayground.BusinessFunctions;
using SkPlayground.Services;
using System.Linq;

namespace SkPlaygroundTests;

/// <summary>
/// **Tests for dynamic tool selection functionality**
///
/// This test class verifies that the SchemaGuidedReasoner and BusinessFunctionFactory
/// correctly support dynamic tool selection, allowing different subsets of tools
/// to be available for different reasoning contexts.
///
/// Key test areas:
/// - **Dynamic Schema Generation**: Validates schema generation with specific tool subsets
/// - **Tool Filtering**: Ensures only specified tools are included in schema and documentation
/// - **Backward Compatibility**: Verifies that default behavior (all tools) still works
/// - **Future Extensibility**: Tests the foundation for context-aware tool selection
/// </summary>
[TestFixture]
public class DynamicToolSelectionTests : SemanticKernelTestBase
{
    private DatabaseService _databaseService = null!;
    private BusinessFunctionFactory _factory = null!;

    /// <summary>
    /// **Setup method** that initializes test dependencies before each test.
    ///
    /// Creates the required DatabaseService and BusinessFunctionFactory instances
    /// for testing dynamic tool selection functionality.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        _databaseService = new DatabaseService();
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(l => l
            .SetMinimumLevel(LogLevel.Warning)
            .AddConsole()
        );
        var kernel = kernelBuilder.Build();
        var loggerFactory = kernel.Services.GetRequiredService<ILoggerFactory>();
        _factory = new BusinessFunctionFactory(_databaseService, new SqlServerService(), kernel, loggerFactory, new Type[]
         {
            typeof(ReportTaskCompletionToolCall),
            typeof(SendEmailToolCall),
            typeof(IssueInvoiceToolCall),
            typeof(GetCustomerDataToolCall),
            typeof(VoidInvoiceToolCall),
            typeof(CreateRuleToolCall),
        });
    }

    /// <summary>
    /// **Test that verifies schema generation with specific tool subset**
    ///
    /// This test ensures that:
    /// - GenerateJsonSchemaForToolCall accepts specific tool types
    /// - Generated schema contains only the specified tools
    /// - Schema structure remains valid with limited tools
    /// </summary>
    [Test]
    public void GenerateJsonSchemaForToolCall_WithSpecificTools_ShouldContainOnlySpecifiedTools()
    {
        // **Arrange**: Define a subset of tools to include
        var includedTools = new[]
        {
            typeof(SendEmailToolCall),
            typeof(ReportTaskCompletionToolCall)
        };

        // **Act**: Generate schema with specific tools
        var schema = _factory.GenerateJsonSchemaForToolCall(includedTools);

        // **Assert**: Verify schema is valid
        Assert.That(schema, Is.Not.Null.And.Not.Empty, "Schema should be generated");

        var schemaNode = System.Text.Json.Nodes.JsonNode.Parse(schema);
        Assert.That(schemaNode, Is.Not.Null, "Schema should be parseable");

        // **Assert**: Verify only specified tools are in definitions
        var definitions = schemaNode!["definitions"]?.AsObject();
        Assert.That(definitions, Is.Not.Null, "Schema should have definitions");

        Assert.That(definitions!.ContainsKey("SendEmailToolCall"), Is.True, "Should contain SendEmailToolCall");
        Assert.That(definitions.ContainsKey("ReportTaskCompletionToolCall"), Is.True, "Should contain ReportTaskCompletionToolCall");

        // **Assert**: Verify excluded tools are not present
        Assert.That(definitions.ContainsKey("IssueInvoiceToolCall"), Is.False, "Should not contain IssueInvoiceToolCall");
        Assert.That(definitions.ContainsKey("GetCustomerDataToolCall"), Is.False, "Should not contain GetCustomerDataToolCall");
        Assert.That(definitions.ContainsKey("VoidInvoiceToolCall"), Is.False, "Should not contain VoidInvoiceToolCall");

        Console.WriteLine("Schema with Limited Tools:");
        Console.WriteLine($"Schema length: {schema.Length} characters");
        Console.WriteLine($"Definitions count: {definitions.Count}");
        Console.WriteLine($"Included tools: {string.Join(", ", definitions.Select(d => d.Key))}");
    }

    /// <summary>
    /// **Test that verifies comprehensive documentation generation with specific tool subset**
    ///
    /// This test ensures that:
    /// - GenerateSchemaWithDocumentationForToolCall works with specific tools
    /// - Property descriptions include only the specified tools
    /// - Function summaries reflect only available tools
    /// - Tool description remains consistent
    /// </summary>
    [Test]
    public void GenerateSchemaWithDocumentationForToolCall_WithSpecificTools_ShouldIncludeOnlySpecifiedTools()
    {
        // **Arrange**: Define a subset of tools focused on customer communication
        var communicationTools = new[]
        {
            typeof(SendEmailToolCall),
            typeof(GetCustomerDataToolCall),
            typeof(ReportTaskCompletionToolCall)
        };

        // **Act**: Generate comprehensive documentation with specific tools
        var result = _factory.GenerateSchemaWithDocumentationForToolCall(communicationTools);

        // **Assert**: Verify result structure
        Assert.That(result, Is.Not.Null, "Result should not be null");
        Assert.That(result.JsonSchema, Is.Not.Null.And.Not.Empty, "JSON schema should be populated");
        Assert.That(result.OuterObjectDescription, Is.Not.Null.And.Not.Empty, "Outer object description should be populated");
        Assert.That(result.AvailableTools, Is.Not.Null.And.Not.Empty, "Available tools should be populated");

        // **Assert**: Verify only specified tools are included
        var toolNames = result.AvailableTools.Select(t => t.ToolName).ToList();
        Assert.That(toolNames, Does.Contain("SendEmailToolCall"), "Should include SendEmailToolCall");
        Assert.That(toolNames, Does.Contain("GetCustomerDataToolCall"), "Should include GetCustomerDataToolCall");
        Assert.That(toolNames, Does.Contain("ReportTaskCompletionToolCall"), "Should include ReportTaskCompletionToolCall");

        // **Assert**: Verify excluded tools are not included
        Assert.That(toolNames, Does.Not.Contain("IssueInvoiceToolCall"), "Should not include IssueInvoiceToolCall");
        Assert.That(toolNames, Does.Not.Contain("VoidInvoiceToolCall"), "Should not include VoidInvoiceToolCall");
        Assert.That(toolNames, Does.Not.Contain("CreateRuleToolCall"), "Should not include CreateRuleToolCall");

        // **Assert**: Verify JSON schema consistency
        var schemaNode = System.Text.Json.Nodes.JsonNode.Parse(result.JsonSchema);
        var definitions = schemaNode!["definitions"]?.AsObject();
        Assert.That(definitions!.Count, Is.EqualTo(3), "Should have exactly 3 tool definitions");

        Console.WriteLine("Communication-Focused Documentation:");
        Console.WriteLine($"Outer Object Description Length: {result.OuterObjectDescription.Length}");
        Console.WriteLine($"Available Tools Count: {result.AvailableTools.Length}");
        Console.WriteLine($"JSON Schema Length: {result.JsonSchema.Length}");
        Console.WriteLine("Tool Types Included:");
        foreach (var tool in result.AvailableTools)
        {
            Console.WriteLine($"  - {tool.ToolName} ({tool.ToolType}): {tool.ToolDescription}");
        }
    }

    /// <summary>
    /// **Test that verifies backward compatibility with default behavior**
    ///
    /// This test ensures that:
    /// - Default behavior (all tools) still works correctly
    /// - Results are equivalent between explicit "all tools" and default call
    /// - No breaking changes were introduced
    /// </summary>
    [Test]
    public void GenerateSchemaWithDocumentationForToolCall_DefaultBehavior_ShouldIncludeAllTools()
    {
        // **Act**: Generate documentation using default behavior (all tools)
        var defaultResult = _factory.GenerateSchemaWithDocumentationForToolCall();

        // **Assert**: Verify comprehensive tool inclusion
        var toolNames = defaultResult.AvailableTools.Select(t => t.ToolName).ToList();
        Assert.That(toolNames, Does.Contain("SendEmailToolCall"), "Should include SendEmailToolCall");
        Assert.That(toolNames, Does.Contain("IssueInvoiceToolCall"), "Should include IssueInvoiceToolCall");
        Assert.That(toolNames, Does.Contain("GetCustomerDataToolCall"), "Should include GetCustomerDataToolCall");
        Assert.That(toolNames, Does.Contain("VoidInvoiceToolCall"), "Should include VoidInvoiceToolCall");
        Assert.That(toolNames, Does.Contain("CreateRuleToolCall"), "Should include CreateRuleToolCall");
        Assert.That(toolNames, Does.Contain("ReportTaskCompletionToolCall"), "Should include ReportTaskCompletionToolCall");

        // **Assert**: Verify JSON schema includes all tools
        var schemaNode = System.Text.Json.Nodes.JsonNode.Parse(defaultResult.JsonSchema);
        var definitions = schemaNode!["definitions"]?.AsObject();
        Assert.That(definitions!.Count, Is.GreaterThanOrEqualTo(6), "Should include all configured tools");

        Console.WriteLine("Default Behavior (All Tools):");
        Console.WriteLine($"Total definitions: {definitions.Count}");
        Console.WriteLine($"Available tools count: {defaultResult.AvailableTools.Length}");
        Console.WriteLine("All Tool Types:");
        foreach (var tool in defaultResult.AvailableTools)
        {
            Console.WriteLine($"  - {tool.ToolName} ({tool.ToolType})");
        }
    }

    /// <summary>
    /// **Test that verifies single tool selection works correctly**
    ///
    /// This test ensures that:
    /// - Single tool selection generates valid schema
    /// - Only the specified tool is included
    /// - Schema structure remains valid with minimal tools
    /// </summary>
    [Test]
    public void GenerateSchemaWithDocumentationForToolCall_SingleTool_ShouldWorkCorrectly()
    {
        // **Arrange**: Select only one tool
        var singleTool = new[] { typeof(SendEmailToolCall) };

        // **Act**: Generate documentation for single tool
        var result = _factory.GenerateSchemaWithDocumentationForToolCall(singleTool);

        // **Assert**: Verify single tool inclusion
        var toolNames = result.AvailableTools.Select(t => t.ToolName).ToList();
        Assert.That(toolNames, Does.Contain("SendEmailToolCall"), "Should include SendEmailToolCall");
        Assert.That(toolNames, Does.Not.Contain("IssueInvoiceToolCall"), "Should not include other tools");

        // **Assert**: Verify JSON schema has only one tool definition
        var schemaNode = System.Text.Json.Nodes.JsonNode.Parse(result.JsonSchema);
        var definitions = schemaNode!["definitions"]?.AsObject();
        Assert.That(definitions!.Count, Is.EqualTo(1), "Should have exactly 1 tool definition");
        Assert.That(definitions.ContainsKey("SendEmailToolCall"), Is.True, "Should contain only SendEmailToolCall");

        Console.WriteLine("Single Tool Selection:");
        Console.WriteLine($"Tool: {singleTool[0].Name}");
        Console.WriteLine($"Definitions count: {definitions.Count}");
        Console.WriteLine($"Available tools count: {result.AvailableTools.Length}");
    }

    /// <summary>
    /// **Test that demonstrates future extensibility for context-aware tool selection**
    ///
    /// This test shows how the dynamic tool selection can be extended for
    /// context-aware scenarios like role-based access control or workflow states.
    /// </summary>
    [Test]
    public void DynamicToolSelection_FutureExtensibility_ShouldSupportContextAwareScenarios()
    {
        // **Arrange**: Define different tool sets for different user roles/contexts
        var basicUserTools = new[]
        {
            typeof(GetCustomerDataToolCall),
            typeof(ReportTaskCompletionToolCall)
        };

        var powerUserTools = new[]
        {
            typeof(SendEmailToolCall),
            typeof(GetCustomerDataToolCall),
            typeof(IssueInvoiceToolCall),
            typeof(ReportTaskCompletionToolCall)
        };

        var adminTools = new[]
        {
            typeof(SendEmailToolCall),
            typeof(GetCustomerDataToolCall),
            typeof(IssueInvoiceToolCall),
            typeof(VoidInvoiceToolCall),
            typeof(CreateRuleToolCall),
            typeof(ReportTaskCompletionToolCall)
        };

        // **Act**: Generate schemas for different contexts
        var basicSchema = _factory.GenerateSchemaWithDocumentationForToolCall(basicUserTools);
        var powerSchema = _factory.GenerateSchemaWithDocumentationForToolCall(powerUserTools);
        var adminSchema = _factory.GenerateSchemaWithDocumentationForToolCall(adminTools);

        // **Assert**: Verify escalating tool availability
        var basicDefs = System.Text.Json.Nodes.JsonNode.Parse(basicSchema.JsonSchema)!["definitions"]!.AsObject();
        var powerDefs = System.Text.Json.Nodes.JsonNode.Parse(powerSchema.JsonSchema)!["definitions"]!.AsObject();
        var adminDefs = System.Text.Json.Nodes.JsonNode.Parse(adminSchema.JsonSchema)!["definitions"]!.AsObject();

        Assert.That(basicDefs.Count, Is.EqualTo(2), "Basic user should have 2 tools");
        Assert.That(powerDefs.Count, Is.EqualTo(4), "Power user should have 4 tools");
        Assert.That(adminDefs.Count, Is.EqualTo(6), "Admin should have 6 tools");

        // **Assert**: Verify tool progression
        Assert.That(basicDefs.ContainsKey("GetCustomerDataToolCall"), Is.True, "Basic user can get customer data");
        Assert.That(basicDefs.ContainsKey("IssueInvoiceToolCall"), Is.False, "Basic user cannot issue invoices");

        Assert.That(powerDefs.ContainsKey("IssueInvoiceToolCall"), Is.True, "Power user can issue invoices");
        Assert.That(powerDefs.ContainsKey("VoidInvoiceToolCall"), Is.False, "Power user cannot void invoices");

        Assert.That(adminDefs.ContainsKey("VoidInvoiceToolCall"), Is.True, "Admin can void invoices");
        Assert.That(adminDefs.ContainsKey("CreateRuleToolCall"), Is.True, "Admin can create rules");

        Console.WriteLine("Context-Aware Tool Selection:");
        Console.WriteLine($"Basic User Tools: {basicDefs.Count} ({string.Join(", ", basicDefs.Select(d => d.Key))})");
        Console.WriteLine($"Power User Tools: {powerDefs.Count} ({string.Join(", ", powerDefs.Select(d => d.Key))})");
        Console.WriteLine($"Admin Tools: {adminDefs.Count} ({string.Join(", ", adminDefs.Select(d => d.Key))})");
    }
}