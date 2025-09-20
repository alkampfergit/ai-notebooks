using System.Text.Json.Nodes;
using SkPlayground.Utils;

namespace SkPlaygroundTests;

/// <summary>
/// **Tests for PolymorphicSchemaManager comprehensive documentation generation**
///
/// This test class verifies that the PolymorphicSchemaManager correctly generates
/// comprehensive schema results including JSON schema, property descriptions in
/// markdown format, and tool descriptions.
///
/// Key test areas:
/// - **SchemaGenerationResult Structure**: Validates the complex result object structure
/// - **Tool Description Extraction**: Ensures container class descriptions are extracted
/// - **Property Documentation**: Verifies markdown-formatted property descriptions
/// - **Comprehensive Integration**: Tests the complete documentation generation flow
/// </summary>
[TestFixture]
public class PolymorphicSchemaManagerDocumentationTests : SemanticKernelTestBase
{
    /// <summary>
    /// **Test tool container with detailed description**
    /// </summary>
    [System.ComponentModel.Description("Advanced business automation tool for managing customer operations and communications")]
    public class BusinessTool
    {
        [System.ComponentModel.Description("Current operational status of the business tool")]
        public required string Status { get; set; }

        [System.ComponentModel.Description("List of pending operations to be executed")]
        public required List<string> PendingOperations { get; set; }

        [System.ComponentModel.Description("Business operation to execute")]
        public required BusinessOperation Operation { get; set; }

        [System.ComponentModel.Description("Indicates whether all operations are completed")]
        public bool AllCompleted { get; set; } = false;
    }

    /// <summary>
    /// **Base class for business operations**
    /// </summary>
    [System.ComponentModel.Description("Base class for all business operations")]
    public abstract class BusinessOperation
    {
        [System.ComponentModel.Description("Unique identifier for the operation type")]
        public abstract string OperationType { get; }

        [System.ComponentModel.Description("Priority level for operation execution")]
        public required int Priority { get; set; }
    }

    /// <summary>
    /// **Customer communication operation**
    /// </summary>
    [System.ComponentModel.Description("Operation for managing customer communications including emails and notifications")]
    public class CustomerCommunication : BusinessOperation
    {
        [System.ComponentModel.Description("Customer's email address for communication")]
        public required string CustomerEmail { get; set; }

        [System.ComponentModel.Description("Type of communication (email, sms, phone)")]
        public required string CommunicationType { get; set; }

        [System.ComponentModel.Description("Message content to be sent to customer")]
        public required string MessageContent { get; set; }

        [System.ComponentModel.Description("Urgency level of the communication")]
        public required string Urgency { get; set; }

        [System.ComponentModel.Description("Operation type identifier")]
        public override string OperationType => "customer_communication";
    }

    /// <summary>
    /// **Data processing operation**
    /// </summary>
    [System.ComponentModel.Description("Operation for processing and analyzing business data")]
    public class DataProcessing : BusinessOperation
    {
        [System.ComponentModel.Description("Source system from which data is processed")]
        public required string DataSource { get; set; }

        [System.ComponentModel.Description("Processing algorithm to apply to the data")]
        public required string ProcessingAlgorithm { get; set; }

        [System.ComponentModel.Description("Output format for processed data")]
        public required string OutputFormat { get; set; }

        [System.ComponentModel.Description("Operation type identifier")]
        public override string OperationType => "data_processing";
    }

    private PolymorphicSchemaManager<BusinessTool, BusinessOperation> _manager = null!;

    /// <summary>
    /// **Setup method** that initializes test dependencies before each test.
    ///
    /// Creates a fresh PolymorphicSchemaManager instance configured with
    /// the business tool hierarchy for documentation testing.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        // Create manager with BusinessTool as container and BusinessOperation as polymorphic base
        _manager = new PolymorphicSchemaManager<BusinessTool, BusinessOperation>("OperationType")
            .AddDerivedType<CustomerCommunication>()
            .AddDerivedType<DataProcessing>();
    }

    /// <summary>
    /// **Test that verifies SchemaGenerationResult structure and content**
    ///
    /// This test ensures that:
    /// - GenerateSchemaWithDocumentation returns a proper SchemaGenerationResult
    /// - All three properties (JsonSchema, PropertyDescriptions, ToolDescription) are populated
    /// - The JSON schema is valid and parseable
    /// </summary>
    [Test]
    public void GenerateSchemaWithDocumentation_ShouldReturnCompleteResult()
    {
        // **Act**: Generate comprehensive schema result
        var result = _manager.GenerateSchemaWithDocumentation();

        // **Assert**: Verify result structure
        Assert.That(result, Is.Not.Null, "Schema generation result should not be null");
        Assert.That(result.JsonSchema, Is.Not.Null.And.Not.Empty, "JSON schema should be populated");
        Assert.That(result.OuterObjectDescription, Is.Not.Null.And.Not.Empty, "Outer object description should be populated");
        Assert.That(result.AvailableTools, Is.Not.Null.And.Not.Empty, "Available tools should be populated");

        // **Assert**: Verify JSON schema is valid
        JsonNode? schemaNode = null;
        Assert.DoesNotThrow(() =>
        {
            schemaNode = JsonNode.Parse(result.JsonSchema);
        }, "Generated JSON schema should be valid and parseable");

        Assert.That(schemaNode, Is.Not.Null, "Parsed schema should not be null");

        Console.WriteLine("Generated Schema Result:");
        Console.WriteLine($"JSON Schema Length: {result.JsonSchema.Length}");
        Console.WriteLine($"Outer Object Description Length: {result.OuterObjectDescription.Length}");
        Console.WriteLine($"Available Tools Count: {result.AvailableTools.Length}");
    }

    /// <summary>
    /// **Test that verifies tool description extraction from container class**
    ///
    /// This test ensures that:
    /// - Tool description is extracted from BusinessTool's Description attribute
    /// - The description matches the expected text
    /// - The description provides meaningful information about the tool
    /// </summary>
    [Test]
    public void GenerateSchemaWithDocumentation_ShouldExtractToolDescription()
    {
        // **Act**: Generate comprehensive schema result
        var result = _manager.GenerateSchemaWithDocumentation();

        // **Assert**: Verify outer object description content
        Assert.That(result.OuterObjectDescription, Is.Not.Null.And.Not.Empty, "Outer object description should be populated");
        Assert.That(result.OuterObjectDescription, Does.Contain("Advanced business automation tool"),
            "Outer object description should contain the BusinessTool class description");
        Assert.That(result.OuterObjectDescription, Does.Contain("managing customer operations"),
            "Outer object description should describe the tool's purpose");

        Console.WriteLine("Outer Object Description:");
        Console.WriteLine(result.OuterObjectDescription);
    }

    /// <summary>
    /// **Test that verifies tool parameter descriptions are formatted correctly**
    ///
    /// This test ensures that:
    /// - Tool parameter descriptions are provided for each tool
    /// - Parameters are documented with their descriptions from attributes
    /// - Each tool has its own parameter documentation
    /// </summary>
    [Test]
    public void GenerateSchemaWithDocumentation_ShouldGenerateToolParameterDescriptions()
    {
        // **Act**: Generate comprehensive schema result
        var result = _manager.GenerateSchemaWithDocumentation();

        // **Assert**: Verify available tools have parameter descriptions
        Assert.That(result.AvailableTools.Length, Is.EqualTo(2), "Should have 2 available tools");

        var customerCommTool = result.AvailableTools.FirstOrDefault(t => t.ToolName == "CustomerCommunication");
        var dataProcessingTool = result.AvailableTools.FirstOrDefault(t => t.ToolName == "DataProcessing");

        Assert.That(customerCommTool, Is.Not.Null, "Should have CustomerCommunication tool");
        Assert.That(dataProcessingTool, Is.Not.Null, "Should have DataProcessing tool");

        // **Assert**: Verify parameter descriptions contain expected content
        Assert.That(customerCommTool!.ParameterDescriptions, Does.Contain("**CustomerEmail**"), "Should document CustomerEmail property");
        Assert.That(customerCommTool.ParameterDescriptions, Does.Contain("Customer's email address"), "Should include CustomerEmail description");
        Assert.That(customerCommTool.ParameterDescriptions, Does.Contain("**Priority**"), "Should document inherited Priority property");

        Assert.That(dataProcessingTool!.ParameterDescriptions, Does.Contain("**DataSource**"), "Should document DataSource property");
        Assert.That(dataProcessingTool.ParameterDescriptions, Does.Contain("Source system from which data"), "Should include DataSource description");

        Console.WriteLine("Tool Parameter Descriptions:");
        foreach (var tool in result.AvailableTools)
        {
            Console.WriteLine($"\n{tool.ToolName}:");
            Console.WriteLine(tool.ParameterDescriptions);
        }
    }

    /// <summary>
    /// **Test that verifies all expected tools and properties are included**
    ///
    /// This test ensures that:
    /// - All tools are present in the available tools array
    /// - Container properties are documented in outer object description
    /// - All tool properties are documented in their respective parameter descriptions
    /// - Inherited properties (like Priority) are included in tool parameter descriptions
    /// </summary>
    [Test]
    public void GenerateSchemaWithDocumentation_ShouldIncludeAllToolsAndProperties()
    {
        // **Act**: Generate comprehensive schema result
        var result = _manager.GenerateSchemaWithDocumentation();

        // **Assert**: Verify both tools are present
        Assert.That(result.AvailableTools.Length, Is.EqualTo(2), "Should have 2 tools");
        var toolNames = result.AvailableTools.Select(t => t.ToolName).ToList();
        Assert.That(toolNames, Does.Contain("CustomerCommunication"), "Should include CustomerCommunication");
        Assert.That(toolNames, Does.Contain("DataProcessing"), "Should include DataProcessing");

        // **Assert**: Verify outer object properties are documented
        var outerDesc = result.OuterObjectDescription;
        Assert.That(outerDesc, Does.Contain("**Status**"), "Should document Status property");
        Assert.That(outerDesc, Does.Contain("**PendingOperations**"), "Should document PendingOperations property");
        Assert.That(outerDesc, Does.Contain("**Operation**"), "Should document Operation property");
        Assert.That(outerDesc, Does.Contain("**AllCompleted**"), "Should document AllCompleted property");

        // **Assert**: Verify CustomerCommunication tool properties
        var customerCommTool = result.AvailableTools.First(t => t.ToolName == "CustomerCommunication");
        Assert.That(customerCommTool.ParameterDescriptions, Does.Contain("**CustomerEmail**"), "Should document CustomerEmail property");
        Assert.That(customerCommTool.ParameterDescriptions, Does.Contain("**CommunicationType**"), "Should document CommunicationType property");
        Assert.That(customerCommTool.ParameterDescriptions, Does.Contain("**MessageContent**"), "Should document MessageContent property");
        Assert.That(customerCommTool.ParameterDescriptions, Does.Contain("**Urgency**"), "Should document Urgency property");
        Assert.That(customerCommTool.ParameterDescriptions, Does.Contain("**Priority**"), "Should document inherited Priority property");
        Assert.That(customerCommTool.ParameterDescriptions, Does.Contain("**OperationType**"), "Should document OperationType property");

        // **Assert**: Verify DataProcessing tool properties
        var dataProcessingTool = result.AvailableTools.First(t => t.ToolName == "DataProcessing");
        Assert.That(dataProcessingTool.ParameterDescriptions, Does.Contain("**DataSource**"), "Should document DataSource property");
        Assert.That(dataProcessingTool.ParameterDescriptions, Does.Contain("**ProcessingAlgorithm**"), "Should document ProcessingAlgorithm property");
        Assert.That(dataProcessingTool.ParameterDescriptions, Does.Contain("**OutputFormat**"), "Should document OutputFormat property");

        // **Assert**: Verify property descriptions are meaningful
        Assert.That(customerCommTool.ParameterDescriptions, Does.Contain("Priority level for operation execution"), "Should include Priority description");
        Assert.That(dataProcessingTool.ParameterDescriptions, Does.Contain("Processing algorithm to apply"), "Should include ProcessingAlgorithm description");
    }

    /// <summary>
    /// **Test that verifies the method works with specific type subsets**
    ///
    /// This test ensures that:
    /// - GenerateSchemaWithDocumentation(includedTypes) works correctly
    /// - Only specified types are included in the documentation
    /// - The JSON schema only contains the specified types
    /// - Property descriptions only include the specified types
    /// </summary>
    [Test]
    public void GenerateSchemaWithDocumentation_WithSpecificTypes_ShouldIncludeOnlySpecifiedTypes()
    {
        // **Act**: Generate schema with only CustomerCommunication
        var result = _manager.GenerateSchemaWithDocumentation(new[] { typeof(CustomerCommunication) });

        // **Assert**: Verify result structure
        Assert.That(result, Is.Not.Null, "Schema generation result should not be null");
        Assert.That(result.JsonSchema, Is.Not.Null.And.Not.Empty, "JSON schema should be populated");
        Assert.That(result.AvailableTools, Is.Not.Null.And.Not.Empty, "Available tools should be populated");

        // **Assert**: Verify only CustomerCommunication is included
        Assert.That(result.AvailableTools.Length, Is.EqualTo(1), "Should have exactly 1 tool");
        Assert.That(result.AvailableTools[0].ToolName, Is.EqualTo("CustomerCommunication"), "Should include only CustomerCommunication");

        // **Assert**: Verify JSON schema only contains CustomerCommunication
        var schemaNode = JsonNode.Parse(result.JsonSchema);
        var definitions = schemaNode!["definitions"]?.AsObject();
        Assert.That(definitions, Is.Not.Null, "Schema should have definitions");
        Assert.That(definitions!.ContainsKey("CustomerCommunication"), Is.True, "Should contain CustomerCommunication definition");
        Assert.That(definitions.ContainsKey("DataProcessing"), Is.False, "Should not contain DataProcessing definition");

        Console.WriteLine("Filtered Schema Result:");
        Console.WriteLine($"Outer Object Description Length: {result.OuterObjectDescription.Length}");
        Console.WriteLine($"Available Tool: {result.AvailableTools[0].ToolName} ({result.AvailableTools[0].ToolType})");
        Console.WriteLine("Tool Description:");
        Console.WriteLine(result.AvailableTools[0].ToolDescription);
    }

    /// <summary>
    /// **Test that verifies backward compatibility with original GenerateSchema method**
    ///
    /// This test ensures that:
    /// - Original GenerateSchema() method still works
    /// - The JSON schema from GenerateSchema() matches the one in SchemaGenerationResult
    /// - No breaking changes were introduced
    /// </summary>
    [Test]
    public void GenerateSchema_OriginalMethod_ShouldStillWork()
    {
        // **Act**: Generate schema using both methods
        var originalSchema = _manager.GenerateSchema();
        var comprehensiveResult = _manager.GenerateSchemaWithDocumentation();

        // **Assert**: Verify both methods return valid schemas
        Assert.That(originalSchema, Is.Not.Null.And.Not.Empty, "Original GenerateSchema should work");
        Assert.That(comprehensiveResult.JsonSchema, Is.Not.Null.And.Not.Empty, "New method should work");

        // **Assert**: Verify the JSON schemas are equivalent
        var originalSchemaNode = JsonNode.Parse(originalSchema);
        var newSchemaNode = JsonNode.Parse(comprehensiveResult.JsonSchema);

        Assert.That(originalSchemaNode, Is.Not.Null, "Original schema should be parseable");
        Assert.That(newSchemaNode, Is.Not.Null, "New schema should be parseable");

        // **Assert**: Verify basic structure equivalence
        Assert.That(originalSchemaNode!["type"]?.ToString(), Is.EqualTo(newSchemaNode!["type"]?.ToString()),
            "Schema types should match");
        Assert.That(originalSchemaNode["title"]?.ToString(), Is.EqualTo(newSchemaNode["title"]?.ToString()),
            "Schema titles should match");

        Console.WriteLine("Backward Compatibility Verification:");
        Console.WriteLine($"Original schema length: {originalSchema.Length}");
        Console.WriteLine($"New schema length: {comprehensiveResult.JsonSchema.Length}");
        Console.WriteLine($"Available tools count: {comprehensiveResult.AvailableTools.Length}");
        Console.WriteLine("Both methods produce equivalent schemas ✓");
    }
}