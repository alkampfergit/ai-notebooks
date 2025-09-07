using System.Text.Json;
using System.Text.Json.Nodes;
using SkPlayground.BusinessFunctions;
using SkPlayground.Services;

namespace SkPlaygroundTests;

/// <summary>
/// **Tests for JSON schema generation functionality**
/// 
/// This test class verifies that the BusinessFunctionFactory correctly generates
/// valid JSON schemas for tool calls that can be used with LLM structured responses.
/// 
/// Key test areas:
/// - **Schema Generation**: Validates that GenerateJsonSchemaForToolCall() produces valid JSON
/// - **Schema Structure**: Ensures the schema contains required properties and structure
/// - **JSON Validation**: Confirms the output is valid JSON that can be parsed
/// </summary>
[TestFixture]
public class JsonSchemaTest
{
    private BusinessFunctionFactory _businessFunctionFactory = null!;
    private JsonSerializerOptions _jsonOptions = null!;
    private DatabaseService _databaseService = null!;

    /// <summary>
    /// **Setup method** that initializes test dependencies before each test.
    /// 
    /// Creates fresh instances of:
    /// - JsonSerializerOptions with camelCase naming policy
    /// - DatabaseService for business function dependencies
    /// - BusinessFunctionFactory with proper dependency injection
    /// </summary>
    [SetUp]
    public void Setup()
    {
        // Configure JSON options to match the application configuration
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // Create database service instance
        _databaseService = new DatabaseService();

        // Create business function factory with dependencies
        _businessFunctionFactory = new BusinessFunctionFactory(_jsonOptions, _databaseService);
    }

    /// <summary>
    /// **Test that verifies JSON schema generation produces valid JSON**
    /// 
    /// This test ensures that:
    /// - GenerateJsonSchemaForToolCall() returns a non-null, non-empty string
    /// - The returned string is valid JSON that can be parsed without errors
    /// - The parsed JSON contains expected schema properties
    /// - The schema structure is suitable for LLM structured responses
    /// </summary>
    [Test]
    public void GenerateJsonSchemaForToolCall_ShouldReturnValidJsonSchema()
    {
        // **Act**: Generate JSON schema using the business function factory
        var schemaJson = _businessFunctionFactory.GenerateJsonSchemaForToolCall();

        // **Assert**: Verify schema is not null or empty
        Assert.That(schemaJson, Is.Not.Null, "Generated schema should not be null");
        Assert.That(schemaJson, Is.Not.Empty, "Generated schema should not be empty");

        // **Assert**: Verify the returned string is valid JSON by parsing it
        JsonNode? schemaNode = null;
        Assert.DoesNotThrow(() =>
        {
            schemaNode = JsonNode.Parse(schemaJson);
        }, "Generated schema should be valid JSON that can be parsed without errors");

        // **Assert**: Verify the parsed JSON is not null
        Assert.That(schemaNode, Is.Not.Null, "Parsed schema node should not be null");

        // **Assert**: Verify the schema contains basic JSON Schema properties
        var schemaObject = schemaNode!.AsObject();
        Assert.That(schemaObject.ContainsKey("type"), Is.True, "Schema should contain 'type' property");
        Assert.That(schemaObject.ContainsKey("properties"), Is.True, "Schema should contain 'properties' property");

        // **Assert**: Verify the schema type is "object" as expected for NextStep
        Assert.That(schemaObject["type"]?.GetValue<string>(), Is.EqualTo("object"), "Schema type should be 'object'");

        // **Assert**: Verify additionalProperties is set to false (OpenAI compatibility)
        Assert.That(schemaObject.ContainsKey("additionalProperties"), Is.True, "Schema should contain 'additionalProperties' property");
        Assert.That(schemaObject["additionalProperties"]?.GetValue<bool>(), Is.False, "Schema should have additionalProperties set to false for OpenAI compatibility");

        // **Assert**: Verify the schema contains expected NextStep properties
        var properties = schemaObject["properties"]?.AsObject();
        Assert.That(properties, Is.Not.Null, "Schema properties should not be null");
        Assert.That(properties!.ContainsKey("currentState"), Is.True, "Schema should contain 'currentState' property");
        Assert.That(properties.ContainsKey("planRemainingStepsBrief"), Is.True, "Schema should contain 'planRemainingStepsBrief' property");
        Assert.That(properties.ContainsKey("taskCompleted"), Is.True, "Schema should contain 'taskCompleted' property");
        Assert.That(properties.ContainsKey("toolCall"), Is.True, "Schema should contain 'toolCall' property");
    }

    /// <summary>
    /// **Test that verifies the schema generation is consistent across multiple calls**
    /// 
    /// This test ensures that:
    /// - Multiple calls to GenerateJsonSchemaForToolCall() return identical results
    /// - The schema generation is deterministic and repeatable
    /// - No side effects occur between calls
    /// </summary>
    [Test]
    public void GenerateJsonSchemaForToolCall_ShouldBeConsistentAcrossMultipleCalls()
    {
        // **Act**: Generate schema multiple times
        var schema1 = _businessFunctionFactory.GenerateJsonSchemaForToolCall();
        var schema2 = _businessFunctionFactory.GenerateJsonSchemaForToolCall();
        var schema3 = _businessFunctionFactory.GenerateJsonSchemaForToolCall();

        // **Assert**: Verify all generated schemas are identical
        Assert.That(schema1, Is.EqualTo(schema2), "First and second schema generation should be identical");
        Assert.That(schema2, Is.EqualTo(schema3), "Second and third schema generation should be identical");
        Assert.That(schema1, Is.EqualTo(schema3), "First and third schema generation should be identical");
    }
}