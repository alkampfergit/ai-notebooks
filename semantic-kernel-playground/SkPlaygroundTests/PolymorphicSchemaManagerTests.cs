using System.Text.Json;
using System.Text.Json.Nodes;
using SkPlayground.Utils;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ActualNextStep = SkPlayground.Models.NextStep;
using ActualToolCall = SkPlayground.Models.ToolCall;

namespace SkPlaygroundTests;

/// <summary>
/// **Tests for generic PolymorphicSchemaManager functionality**
/// 
/// This test class verifies that the PolymorphicSchemaManager correctly generates
/// valid JSON schemas for polymorphic types and handles deserialization properly.
/// Uses the NextStep/ToolCall hierarchy as a test case.
/// 
/// Key test areas:
/// - **Schema Generation**: Validates polymorphic schema generation
/// - **Schema Structure**: Ensures proper anyOf patterns and definitions
/// - **Polymorphic Deserialization**: Confirms correct type resolution
/// - **Generic Functionality**: Tests the reflection-based property detection
/// </summary>
[TestFixture]
public class PolymorphicSchemaManagerTests : SemanticKernelTestBase
{
    private PolymorphicSchemaManager<ActualNextStep, ActualToolCall> _manager = null!;

    /// <summary>
    /// **Setup method** that initializes test dependencies before each test.
    /// 
    /// Creates a fresh PolymorphicSchemaManager instance configured with
    /// the NextStep/ToolCall hierarchy for testing.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        // Create manager with NextStep as container and ToolCall as polymorphic base
        _manager = new PolymorphicSchemaManager<ActualNextStep, ActualToolCall>()
            .AddDerivedType<SkPlayground.BusinessFunctions.SendEmailToolCall>()
            .AddDerivedType<SkPlayground.BusinessFunctions.GetCustomerDataToolCall>()
            .AddDerivedType<SkPlayground.BusinessFunctions.IssueInvoiceToolCall>();
    }

    /// <summary>
    /// **Test that verifies generic schema generation produces valid JSON**
    /// 
    /// This test ensures that:
    /// - GenerateSchema() returns a non-null, non-empty string
    /// - The returned string is valid JSON that can be parsed without errors
    /// - The parsed JSON contains expected polymorphic schema properties
    /// </summary>
    [Test]
    public void GenerateSchema_ShouldReturnValidJsonSchema()
    {
        // **Act**: Generate JSON schema using the generic manager
        var schemaJson = _manager.GenerateSchema();

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

        // **Assert**: Verify additionalProperties is set to false (OpenAI compatibility)
        Assert.That(schemaObject.ContainsKey("additionalProperties"), Is.True, "Schema should contain 'additionalProperties' property");
        Assert.That(schemaObject["additionalProperties"]?.GetValue<bool>(), Is.False, "Schema should have additionalProperties set to false for OpenAI compatibility");
    }

    /// <summary>
    /// **Test that verifies the schema contains anyOf pattern for polymorphic types**
    /// 
    /// This test ensures that:
    /// - The toolCall property uses anyOf pattern for polymorphic types
    /// - Expected derived types are included in the anyOf array
    /// - Definitions section contains the derived type schemas
    /// </summary>
    [Test]
    public void GenerateSchema_ShouldContainAnyOfForPolymorphicProperty()
    {
        // **Act**: Generate JSON schema using the generic manager
        var schemaJson = _manager.GenerateSchema();
        var schemaNode = JsonNode.Parse(schemaJson);

        // **Assert**: Navigate to the toolCall property in the schema
        var schemaObject = schemaNode!.AsObject();
        var properties = schemaObject["properties"]?.AsObject();
        var toolCallProperty = properties!["toolCall"]?.AsObject();
        
        Assert.That(toolCallProperty, Is.Not.Null, "toolCall property should be present in schema");

        // **Assert**: Verify that toolCall property contains anyOf for polymorphic types
        Assert.That(toolCallProperty!.ContainsKey("anyOf"), Is.True, "toolCall property should contain 'anyOf' for polymorphic types");

        var anyOfArray = toolCallProperty["anyOf"]?.AsArray();
        Assert.That(anyOfArray, Is.Not.Null, "anyOf should be an array");
        Assert.That(anyOfArray!.Count, Is.EqualTo(3), "anyOf array should contain exactly 3 types (SendEmail, GetCustomerData, IssueInvoice)");

        // **Assert**: Verify definitions section contains expected types
        Assert.That(schemaObject.ContainsKey("definitions"), Is.True, "Schema should contain 'definitions' section");
        var definitions = schemaObject["definitions"]?.AsObject();
        
        Assert.That(definitions!.ContainsKey("SendEmailToolCall"), Is.True, "Definitions should contain SendEmailToolCall");
        Assert.That(definitions.ContainsKey("GetCustomerDataToolCall"), Is.True, "Definitions should contain GetCustomerDataToolCall");
        Assert.That(definitions.ContainsKey("IssueInvoiceToolCall"), Is.True, "Definitions should contain IssueInvoiceToolCall");
    }

    /// <summary>
    /// **Test that verifies schema generation is consistent across multiple calls**
    /// 
    /// This test ensures that:
    /// - Multiple calls to GenerateSchema() return identical results
    /// - The schema generation is deterministic and repeatable
    /// </summary>
    [Test]
    public void GenerateSchema_ShouldBeConsistentAcrossMultipleCalls()
    {
        // **Act**: Generate schema multiple times
        var schema1 = _manager.GenerateSchema();
        var schema2 = _manager.GenerateSchema();
        var schema3 = _manager.GenerateSchema();

        // **Assert**: Verify all generated schemas are identical
        Assert.That(schema1, Is.EqualTo(schema2), "First and second schema generation should be identical");
        Assert.That(schema2, Is.EqualTo(schema3), "Second and third schema generation should be identical");
        Assert.That(schema1, Is.EqualTo(schema3), "First and third schema generation should be identical");
    }

    /// <summary>
    /// **Test that verifies selective schema generation with specific types**
    /// 
    /// This test ensures that:
    /// - GenerateSchema(includedTypes) only includes specified types
    /// - The anyOf array contains only the requested types
    /// </summary>
    [Test]
    public void GenerateSchema_WithIncludedTypes_ShouldOnlyIncludeSpecifiedTypes()
    {
        // **Arrange**: Specify only SendEmail and IssueInvoice types
        var includedTypes = new[] { typeof(SkPlayground.BusinessFunctions.SendEmailToolCall), typeof(SkPlayground.BusinessFunctions.IssueInvoiceToolCall) };

        // **Act**: Generate schema with specific types
        var schemaJson = _manager.GenerateSchema(includedTypes);
        var schemaNode = JsonNode.Parse(schemaJson);

        // **Assert**: Verify anyOf contains only specified types
        var schemaObject = schemaNode!.AsObject();
        var properties = schemaObject["properties"]?.AsObject();
        var toolCallProperty = properties!["toolCall"]?.AsObject();
        var anyOfArray = toolCallProperty!["anyOf"]?.AsArray();
        
        Assert.That(anyOfArray!.Count, Is.EqualTo(2), "anyOf array should contain exactly 2 types");

        // **Assert**: Verify definitions contains only specified types
        var definitions = schemaObject["definitions"]?.AsObject();
        Assert.That(definitions!.ContainsKey("SendEmailToolCall"), Is.True, "Definitions should contain SendEmailToolCall");
        Assert.That(definitions.ContainsKey("IssueInvoiceToolCall"), Is.True, "Definitions should contain IssueInvoiceToolCall");
        Assert.That(definitions.ContainsKey("GetCustomerDataToolCall"), Is.False, "Definitions should NOT contain GetCustomerDataToolCall");
    }

    /// <summary>
    /// **Test that verifies polymorphic deserialization works correctly**
    /// 
    /// This test ensures that:
    /// - JSON with polymorphic content deserializes to correct concrete types
    /// - Properties are correctly populated on deserialized objects
    /// </summary>
    [Test]
    public void DeserializeFromJson_ShouldCorrectlyDeserializePolymorphicTypes()
    {
        // **Arrange**: Create JSON with SendEmailToolCall
        var json = """
        {
            "currentState": "Processing email request",
            "planRemainingStepsBrief": ["Send confirmation email", "Update customer record"],
            "taskCompleted": false,
            "toolCall": {
                "type": "send_email_tool_call",
                "subject": "Order Confirmation",
                "message": "Your order has been processed",
                "recipientEmail": "customer@example.com",
                "files": ["invoice.pdf"]
            }
        }
        """;

        // **Act**: Deserialize using generic manager
        var result = _manager.DeserializeFromJson(json);

        // **Assert**: Verify deserialization succeeded
        Assert.That(result, Is.Not.Null, "Deserialization should succeed");
        Assert.That(result!.CurrentState, Is.EqualTo("Processing email request"), "CurrentState should be correctly deserialized");
        Assert.That(result.TaskCompleted, Is.False, "TaskCompleted should be correctly deserialized");
        
        // **Assert**: Verify polymorphic property is correctly typed
        Assert.That(result.ToolCall, Is.InstanceOf<SkPlayground.BusinessFunctions.SendEmailToolCall>(), "ToolCall should be deserialized as SendEmailToolCall");
        
        var emailCall = result.ToolCall as SkPlayground.BusinessFunctions.SendEmailToolCall;
        Assert.That(emailCall, Is.Not.Null, "Should be able to cast ToolCall to SendEmailToolCall");
        Assert.That(emailCall!.Subject, Is.EqualTo("Order Confirmation"), "Subject should be correctly deserialized");
        Assert.That(emailCall.Message, Is.EqualTo("Your order has been processed"), "Message should be correctly deserialized");
        Assert.That(emailCall.RecipientEmail, Is.EqualTo("customer@example.com"), "RecipientEmail should be correctly deserialized");
        Assert.That(emailCall.Files, Contains.Item("invoice.pdf"), "Files should contain expected item");
    }

    /// <summary>
    /// **Test that verifies error handling for unknown discriminator values**
    /// 
    /// This test ensures that:
    /// - Unknown discriminator values throw appropriate exceptions
    /// - Error messages provide helpful information about available types
    /// </summary>
    [Test]
    public void DeserializeFromJson_WithUnknownDiscriminator_ShouldThrowException()
    {
        // **Arrange**: Create JSON with unknown discriminator
        var json = """
        {
            "currentState": "Processing request",
            "planRemainingStepsBrief": ["Process request"],
            "taskCompleted": false,
            "toolCall": {
                "type": "unknown_tool_call",
                "someProperty": "value"
            }
        }
        """;

        // **Act & Assert**: Verify exception is thrown
        var ex = Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() =>
        {
            _manager.DeserializeFromJson(json);
        });

        Assert.That(ex!.Message, Contains.Substring("Unknown ToolCall type: 'unknown_tool_call'"), "Error message should indicate unknown type");
        Assert.That(ex.Message, Contains.Substring("send_email_tool_call"), "Error message should list available types");
    }

    /// <summary>
    /// **Test that verifies reflection correctly identifies polymorphic property**
    /// 
    /// This test ensures that:
    /// - The manager correctly identifies the ToolCall property in NextStep
    /// - Property information is accessible and correct
    /// </summary>
    [Test]
    public void Constructor_ShouldCorrectlyIdentifyPolymorphicProperty()
    {
        // **Act & Assert**: Verify polymorphic property was identified
        Assert.That(_manager.PolymorphicProperty, Is.Not.Null, "Polymorphic property should be identified");
        Assert.That(_manager.PolymorphicProperty.Name, Is.EqualTo("ToolCall"), "Property name should be 'ToolCall'");
        Assert.That(_manager.PolymorphicProperty.PropertyType, Is.EqualTo(typeof(ActualToolCall)), "Property type should be ToolCall");
    }

    /// <summary>
    /// **Test that verifies error handling when no polymorphic property exists**
    /// 
    /// This test ensures that:
    /// - Constructor throws exception when container type has no polymorphic property
    /// - Error message provides helpful information
    /// </summary>
    [Test]
    public void Constructor_WithoutPolymorphicProperty_ShouldThrowException()
    {
        // **Act & Assert**: Try to create manager with incompatible types
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            new PolymorphicSchemaManager<string, ActualToolCall>();
        });

        Assert.That(ex!.Message, Contains.Substring("No property of type ToolCall found in String"), "Error message should indicate missing property");
    }

    /// <summary>
    /// **Test that verifies discriminator value generation**
    /// 
    /// This test ensures that:
    /// - Discriminator values are correctly generated from type names
    /// - PascalCase is converted to snake_case as expected
    /// </summary>
    [Test]
    public void GetDiscriminatorValue_ShouldConvertPascalCaseToSnakeCase()
    {
        // **Act & Assert**: Verify discriminator conversion
        var sendEmailDiscriminator = PolymorphicSchemaManager<ActualNextStep, ActualToolCall>.GetDiscriminatorValue(typeof(SkPlayground.BusinessFunctions.SendEmailToolCall));
        var getCustomerDiscriminator = PolymorphicSchemaManager<ActualNextStep, ActualToolCall>.GetDiscriminatorValue(typeof(SkPlayground.BusinessFunctions.GetCustomerDataToolCall));
        var issueInvoiceDiscriminator = PolymorphicSchemaManager<ActualNextStep, ActualToolCall>.GetDiscriminatorValue(typeof(SkPlayground.BusinessFunctions.IssueInvoiceToolCall));

        Assert.That(sendEmailDiscriminator, Is.EqualTo("send_email_tool_call"), "SendEmailToolCall should convert to send_email_tool_call");
        Assert.That(getCustomerDiscriminator, Is.EqualTo("get_customer_data_tool_call"), "GetCustomerDataToolCall should convert to get_customer_data_tool_call");
        Assert.That(issueInvoiceDiscriminator, Is.EqualTo("issue_invoice_tool_call"), "IssueInvoiceToolCall should convert to issue_invoice_tool_call");
    }

    /// <summary>
    /// **Test that verifies all properties are marked as required for OpenAI compatibility**
    /// 
    /// This test ensures that:
    /// - Generated schemas have all properties in required arrays
    /// - This satisfies OpenAI's additionalProperties: false requirement
    /// </summary>
    [Test]
    public void GenerateSchema_ShouldMarkAllPropertiesAsRequired()
    {
        // **Act**: Generate schema
        var schemaJson = _manager.GenerateSchema();
        var schemaNode = JsonNode.Parse(schemaJson);

        // **Assert**: Verify root schema has all properties required
        var schemaObject = schemaNode!.AsObject();
        var properties = schemaObject["properties"]?.AsObject();
        var required = schemaObject["required"]?.AsArray();

        Assert.That(required, Is.Not.Null, "Schema should have required array");
        Assert.That(properties, Is.Not.Null, "Schema should have properties");

        // **Assert**: Verify all properties are in required array
        foreach (var property in properties!)
        {
            var propertyName = property.Key;
            var isRequired = required!.Any(r => r?.GetValue<string>() == propertyName);
            Assert.That(isRequired, Is.True, $"Property '{propertyName}' should be in required array for OpenAI compatibility");
        }

        // **Assert**: Verify derived type schemas also have all properties required
        var definitions = schemaObject["definitions"]?.AsObject();
        if (definitions != null)
        {
            foreach (var definition in definitions)
            {
                var defSchema = definition.Value?.AsObject();
                if (defSchema != null && defSchema.ContainsKey("properties"))
                {
                    var defProperties = defSchema["properties"]?.AsObject();
                    var defRequired = defSchema["required"]?.AsArray();

                    if (defProperties != null && defRequired != null)
                    {
                        foreach (var defProperty in defProperties)
                        {
                            var defPropertyName = defProperty.Key;
                            var isDefRequired = defRequired.Any(r => r?.GetValue<string>() == defPropertyName);
                            Assert.That(isDefRequired, Is.True, $"Property '{defPropertyName}' in definition '{definition.Key}' should be in required array for OpenAI compatibility");
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// **Real LLM test that validates schema generation and polymorphic deserialization with OpenAI**
    /// 
    /// This test mimics real-world usage by:
    /// - Generating a schema using the generic manager
    /// - Making an actual LLM call with structured output
    /// - Verifying the response deserializes correctly to the expected polymorphic type
    /// </summary>
    [Test]
    public async Task GenerateSchema_RealLLMCall_PolymorphicDeserialization()
    {
        // Skip test if no API key is available
        var apiKey = Dotenv.Get("OPENAI_API_KEY");
        var endpoint = Dotenv.Get("AZURE_ENDPOINT");
        if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(endpoint))
        {
            Assert.Ignore("Skipping LLM test - no API key or endpoint configured");
        }

        try
        {
            // **Arrange**: Generate schema and prepare LLM call
            var schemaJson = _manager.GenerateSchema();
            var completionService = GetCompletionService(apiKey, endpoint, schemaJson);
            
            Console.WriteLine("Generated Schema:");
            Console.WriteLine(schemaJson);
            
            // **Act**: Make LLM call with structured output
            var prompt = """
                You need to process a customer order confirmation. The customer email is customer@example.com.
                Create a next step that involves sending a confirmation email with subject "Order Confirmation #12345"
                and message "Thank you for your order. Your items will be shipped soon."
                
                Current state should be "Preparing order confirmation"
                Remaining steps should include: ["Send confirmation email", "Update order status"]
                Task is not completed yet.
                """;

            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ResponseFormat = "json_object",
                Temperature = 0.1f,
                MaxTokens = 1000
            };

            var skResponse = await completionService.GetChatMessageContentAsync(chatHistory, executionSettings);
            var responseContent = skResponse.Content;
            Assert.That(responseContent, Is.Not.Null.And.Not.Empty, "LLM should return structured response");

            Console.WriteLine("LLM Response:");
            Console.WriteLine(responseContent);

            // **Assert**: Deserialize and validate the response
            var nextStep = _manager.DeserializeFromJson(responseContent!);
            Assert.That(nextStep, Is.Not.Null, "Response should deserialize to NextStep");
            
            Assert.That(nextStep!.CurrentState, Is.Not.Null.And.Not.Empty, "CurrentState should be populated");
            Assert.That(nextStep.PlanRemainingStepsBrief, Is.Not.Null.And.Not.Empty, "PlanRemainingStepsBrief should be populated");
            Assert.That(nextStep.TaskCompleted, Is.False, "TaskCompleted should be false as requested");
            Assert.That(nextStep.ToolCall, Is.InstanceOf<SkPlayground.BusinessFunctions.SendEmailToolCall>(), "ToolCall should be SendEmailToolCall");

            var sendEmailCall = nextStep.ToolCall as SkPlayground.BusinessFunctions.SendEmailToolCall;
            Assert.That(sendEmailCall, Is.Not.Null, "Should be able to cast ToolCall to SendEmailToolCall");
            Assert.That(sendEmailCall!.Subject, Is.Not.Null.And.Not.Empty, "Email subject should not be empty");
            Assert.That(sendEmailCall.Message, Is.Not.Null.And.Not.Empty, "Email message should not be empty");
            Assert.That(sendEmailCall.RecipientEmail, Is.Not.Null.And.Not.Empty, "Recipient email should not be empty");
            
            Assert.That(sendEmailCall.Subject.ToLower(), Contains.Substring("confirmation").Or.Contains("order"), "Should extract order confirmation subject");
            Assert.That(sendEmailCall.RecipientEmail.ToLower(), Contains.Substring("customer@example.com"), "Should extract recipient email");

            Console.WriteLine($"✅ Successfully extracted NextStep: '{nextStep.CurrentState}' with SendEmailToolCall");
            Console.WriteLine($"✅ Email details: '{sendEmailCall.Subject}' to '{sendEmailCall.RecipientEmail}'");
            Console.WriteLine("✅ Generic polymorphic deserialization successful - ToolCall correctly identified as SendEmailToolCall");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Failed to perform LLM call: {ex.Message}");
        }

        Console.WriteLine("✅ Real LLM call with generic polymorphic schema validation completed successfully!");
    }
}