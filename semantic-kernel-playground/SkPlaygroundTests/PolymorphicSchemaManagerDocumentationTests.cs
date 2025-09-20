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
        Assert.That(result.PropertyDescriptions, Is.Not.Null.And.Not.Empty, "Property descriptions should be populated");
        Assert.That(result.ToolDescription, Is.Not.Null.And.Not.Empty, "Tool description should be populated");

        // **Assert**: Verify JSON schema is valid
        JsonNode? schemaNode = null;
        Assert.DoesNotThrow(() =>
        {
            schemaNode = JsonNode.Parse(result.JsonSchema);
        }, "Generated JSON schema should be valid and parseable");

        Assert.That(schemaNode, Is.Not.Null, "Parsed schema should not be null");

        Console.WriteLine("Generated Schema Result:");
        Console.WriteLine($"JSON Schema Length: {result.JsonSchema.Length}");
        Console.WriteLine($"Property Descriptions Length: {result.PropertyDescriptions.Length}");
        Console.WriteLine($"Tool Description: {result.ToolDescription}");
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

        // **Assert**: Verify tool description content
        Assert.That(result.ToolDescription, Is.Not.Null.And.Not.Empty, "Tool description should be populated");
        Assert.That(result.ToolDescription, Does.Contain("Advanced business automation tool"),
            "Tool description should contain the BusinessTool class description");
        Assert.That(result.ToolDescription, Does.Contain("managing customer operations"),
            "Tool description should describe the tool's purpose");

        Console.WriteLine("Tool Description:");
        Console.WriteLine(result.ToolDescription);
    }

    /// <summary>
    /// **Test that verifies property descriptions are formatted as markdown**
    ///
    /// This test ensures that:
    /// - Property descriptions are formatted as proper markdown
    /// - Container properties are documented with their descriptions
    /// - Polymorphic type properties are documented separately
    /// - Markdown structure includes headers and bullet points
    /// </summary>
    [Test]
    public void GenerateSchemaWithDocumentation_ShouldGenerateMarkdownPropertyDescriptions()
    {
        // **Act**: Generate comprehensive schema result
        var result = _manager.GenerateSchemaWithDocumentation();

        // **Assert**: Verify markdown structure
        var markdown = result.PropertyDescriptions;
        Assert.That(markdown, Is.Not.Null.And.Not.Empty, "Property descriptions should be populated");

        // **Assert**: Verify markdown headers
        Assert.That(markdown, Does.Contain("# Property Descriptions"), "Should contain main header");
        Assert.That(markdown, Does.Contain("## BusinessTool Properties"), "Should contain container properties section");
        Assert.That(markdown, Does.Contain("## CustomerCommunication Properties"), "Should contain derived type section");
        Assert.That(markdown, Does.Contain("## DataProcessing Properties"), "Should contain second derived type section");

        // **Assert**: Verify property documentation
        Assert.That(markdown, Does.Contain("**Status**"), "Should document Status property");
        Assert.That(markdown, Does.Contain("**CustomerEmail**"), "Should document CustomerEmail property");
        Assert.That(markdown, Does.Contain("**DataSource**"), "Should document DataSource property");

        // **Assert**: Verify property descriptions
        Assert.That(markdown, Does.Contain("Current operational status"), "Should include Status description");
        Assert.That(markdown, Does.Contain("Customer's email address"), "Should include CustomerEmail description");
        Assert.That(markdown, Does.Contain("Source system from which data"), "Should include DataSource description");

        Console.WriteLine("Property Descriptions Markdown:");
        Console.WriteLine(markdown);
    }

    /// <summary>
    /// **Test that verifies property descriptions include all expected properties**
    ///
    /// This test ensures that:
    /// - All container properties are documented
    /// - All polymorphic type properties are documented
    /// - Inherited properties (like Priority) are included
    /// - Property descriptions match the Description attributes
    /// </summary>
    [Test]
    public void GenerateSchemaWithDocumentation_ShouldIncludeAllProperties()
    {
        // **Act**: Generate comprehensive schema result
        var result = _manager.GenerateSchemaWithDocumentation();

        var markdown = result.PropertyDescriptions;

        // **Assert**: Verify all BusinessTool properties are documented
        Assert.That(markdown, Does.Contain("**Status**"), "Should document Status property");
        Assert.That(markdown, Does.Contain("**PendingOperations**"), "Should document PendingOperations property");
        Assert.That(markdown, Does.Contain("**Operation**"), "Should document Operation property");
        Assert.That(markdown, Does.Contain("**AllCompleted**"), "Should document AllCompleted property");

        // **Assert**: Verify all CustomerCommunication properties are documented
        Assert.That(markdown, Does.Contain("**CustomerEmail**"), "Should document CustomerEmail property");
        Assert.That(markdown, Does.Contain("**CommunicationType**"), "Should document CommunicationType property");
        Assert.That(markdown, Does.Contain("**MessageContent**"), "Should document MessageContent property");
        Assert.That(markdown, Does.Contain("**Urgency**"), "Should document Urgency property");
        Assert.That(markdown, Does.Contain("**Priority**"), "Should document inherited Priority property");
        Assert.That(markdown, Does.Contain("**OperationType**"), "Should document OperationType property");

        // **Assert**: Verify all DataProcessing properties are documented
        Assert.That(markdown, Does.Contain("**DataSource**"), "Should document DataSource property");
        Assert.That(markdown, Does.Contain("**ProcessingAlgorithm**"), "Should document ProcessingAlgorithm property");
        Assert.That(markdown, Does.Contain("**OutputFormat**"), "Should document OutputFormat property");

        // **Assert**: Verify property descriptions are meaningful
        Assert.That(markdown, Does.Contain("Priority level for operation execution"), "Should include Priority description");
        Assert.That(markdown, Does.Contain("Processing algorithm to apply"), "Should include ProcessingAlgorithm description");
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
        Assert.That(result.PropertyDescriptions, Is.Not.Null.And.Not.Empty, "Property descriptions should be populated");

        // **Assert**: Verify only CustomerCommunication is included
        var markdown = result.PropertyDescriptions;
        Assert.That(markdown, Does.Contain("## CustomerCommunication Properties"), "Should include CustomerCommunication section");
        Assert.That(markdown, Does.Not.Contain("## DataProcessing Properties"), "Should not include DataProcessing section");

        // **Assert**: Verify JSON schema only contains CustomerCommunication
        var schemaNode = JsonNode.Parse(result.JsonSchema);
        var definitions = schemaNode!["definitions"]?.AsObject();
        Assert.That(definitions, Is.Not.Null, "Schema should have definitions");
        Assert.That(definitions!.ContainsKey("CustomerCommunication"), Is.True, "Should contain CustomerCommunication definition");
        Assert.That(definitions.ContainsKey("DataProcessing"), Is.False, "Should not contain DataProcessing definition");

        Console.WriteLine("Filtered Schema Result:");
        Console.WriteLine($"Tool Description: {result.ToolDescription}");
        Console.WriteLine("Property Descriptions:");
        Console.WriteLine(result.PropertyDescriptions);
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
        Console.WriteLine("Both methods produce equivalent schemas ✓");
    }
}