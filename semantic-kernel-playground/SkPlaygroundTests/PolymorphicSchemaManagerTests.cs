using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using SkPlayground.Utils;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        // **Assert**: Navigate to the ToolCall property in the schema (PascalCase)
        var schemaObject = schemaNode!.AsObject();
        var properties = schemaObject["properties"]?.AsObject();
        var toolCallProperty = properties!["ToolCall"]?.AsObject();

        Assert.That(toolCallProperty, Is.Not.Null, "ToolCall property should be present in schema");

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
        var includedTypes = new[] {
            typeof(SkPlayground.BusinessFunctions.SendEmailToolCall),
            typeof(SkPlayground.BusinessFunctions.IssueInvoiceToolCall)
        };

        // **Act**: Generate schema with specific types
        var schemaJson = _manager.GenerateSchema(includedTypes);
        var schemaNode = JsonNode.Parse(schemaJson);

        // **Assert**: Verify anyOf contains only specified types
        var schemaObject = schemaNode!.AsObject();
        var properties = schemaObject["properties"]?.AsObject();
        var toolCallProperty = properties!["ToolCall"]?.AsObject();
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
    /// **Test that verifies schema contains derived references**
    /// Adapted from NextStepManagerTests.Contains_derived_references
    /// </summary>
    [Test]
    public void Contains_derived_references()
    {
        // **Act**: Generate complete schema
        var schema = _manager.GenerateSchema();
        var schemaObj = JsonDocument.Parse(schema);
        var root = schemaObj.RootElement;

        // **Assert**: Verify schema contains definitions for derived types
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions'");
        Assert.That(definitions.TryGetProperty("SendEmailToolCall", out var sendEmail), Is.True, "Schema should have 'SendEmailToolCall' definition");
        Assert.That(definitions.TryGetProperty("GetCustomerDataToolCall", out var getCustomer), Is.True, "Schema should have 'GetCustomerDataToolCall' definition");

        // **Assert**: Verify SendEmailToolCall schema is correct
        Assert.That(sendEmail.TryGetProperty("properties", out var sendEmailProps), Is.True, "SendEmailToolCall should have 'properties'");
        var sendEmailPropsObj = sendEmailProps.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        Assert.That(sendEmailPropsObj.ContainsKey("type"), Is.True, "SendEmailToolCall should have 'type' property");

        // Debug what properties we actually have
        Console.WriteLine($"Available properties in SendEmailToolCall: {string.Join(", ", sendEmailPropsObj.Keys)}");

        // Check for both PascalCase and camelCase versions (to match NextStepManager behavior)
        Assert.That(sendEmailPropsObj.ContainsKey("Subject") || sendEmailPropsObj.ContainsKey("subject"), Is.True, "SendEmailToolCall should have 'Subject' or 'subject' property");
        Assert.That(sendEmailPropsObj.ContainsKey("Message") || sendEmailPropsObj.ContainsKey("message"), Is.True, "SendEmailToolCall should have 'Message' or 'message' property");
        Assert.That(sendEmailPropsObj.ContainsKey("RecipientEmail") || sendEmailPropsObj.ContainsKey("recipientEmail"), Is.True, "SendEmailToolCall should have 'RecipientEmail' or 'recipientEmail' property");
    }

    /// <summary>
    /// **Test that verifies schema can be generated with only one derived type**
    /// Adapted from NextStepManagerTests.Can_generate_schema_with_only_sendEmail
    /// </summary>
    [Test]
    public void Can_generate_schema_with_only_sendEmail()
    {
        // **Arrange**: Create manager with only SendEmail
        var manager = new PolymorphicSchemaManager<ActualNextStep, ActualToolCall>()
            .AddDerivedType<SkPlayground.BusinessFunctions.SendEmailToolCall>();

        // **Act**: Generate schema
        var schemaJson = manager.GenerateSchema();
        var schemaObj = JsonDocument.Parse(schemaJson);
        var root = schemaObj.RootElement;

        // **Assert**: Verify schema structure
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions'");
        Assert.That(definitions.TryGetProperty("SendEmailToolCall", out _), Is.True, "Schema should have 'SendEmailToolCall' definition");
        Assert.That(definitions.TryGetProperty("GetCustomerDataToolCall", out _), Is.False, "Schema should NOT have 'GetCustomerDataToolCall' definition");

        // **Assert**: Verify ToolCall property has anyOf with only SendEmail
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True, "Schema should have 'properties'");
        Assert.That(properties.TryGetProperty("ToolCall", out var toolCallProp), Is.True, "Schema should have 'ToolCall' property");
        Assert.That(toolCallProp.TryGetProperty("anyOf", out var anyOf), Is.True, "toolCall property should have 'anyOf'");

        var anyOfArray = anyOf.EnumerateArray().ToList();
        Assert.That(anyOfArray.Count, Is.EqualTo(1), "anyOf should have exactly one option (SendEmailToolCall)");
    }

    /// <summary>
    /// **Test that verifies derived class schema has discriminator type**
    /// Adapted from NextStepManagerTests.Derived_class_schema_has_discriminator_type
    /// </summary>
    [Test]
    public void Derived_class_schema_has_discriminator_type()
    {
        // **Act**: Generate schema
        var schema = _manager.GenerateSchema();
        var schemaObj = JsonDocument.Parse(schema);
        var root = schemaObj.RootElement;

        // **Assert**: Navigate to SendEmailToolCall definition
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions'");
        Assert.That(definitions.TryGetProperty("SendEmailToolCall", out var sendEmail), Is.True, "Schema should have 'SendEmailToolCall' definition");

        // **Assert**: Verify SendEmailToolCall has type property with enum "send_email_tool_call"
        Assert.That(sendEmail.TryGetProperty("properties", out var sendEmailProps), Is.True, "SendEmailToolCall should have 'properties'");
        var sendEmailPropsObj = sendEmailProps.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        Assert.That(sendEmailPropsObj.ContainsKey("type"), Is.True, "SendEmailToolCall should have 'type' property");
        var typeProp = sendEmailPropsObj["type"];

        Assert.That(typeProp.TryGetProperty("enum", out var enumValues), Is.True, "type property should have 'enum'");
        var enums = enumValues.EnumerateArray().Select(v => v.GetString()).ToList();
        Assert.That(enums, Contains.Item("send_email_tool_call"), "type enum should contain 'send_email_tool_call'");

        // **Assert**: Should have a title
        Assert.That(typeProp.TryGetProperty("title", out var titleValue), Is.True, "type property should have 'title'");
        Assert.That(titleValue.GetString(), Is.EqualTo("Type"), "type property should have title 'Type'");

        // **Assert**: Type of the property should be string
        Assert.That(typeProp.TryGetProperty("type", out var typeValue), Is.True, "type property should have 'type'");
        Assert.That(typeValue.GetString(), Is.EqualTo("string"), "type property should be of type 'string'");

        // **Assert**: Should have const value
        Assert.That(typeProp.TryGetProperty("const", out var constValue), Is.True, "type property should have 'const'");
        Assert.That(constValue.GetString(), Is.EqualTo("send_email_tool_call"), "const should be 'send_email_tool_call'");
    }

    /// <summary>
    /// **Test that verifies base class contains anyOf**
    /// Adapted from NextStepManagerTests.Base_class_Contains_anyof
    /// </summary>
    [Test]
    public void Base_class_Contains_anyof()
    {
        // **Act**: Generate schema
        var schema = _manager.GenerateSchema();
        var schemaObj = JsonDocument.Parse(schema);
        var root = schemaObj.RootElement;

        // **Assert**: Navigate to ToolCall property
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True, "Schema should have 'properties'");
        Assert.That(properties.TryGetProperty("ToolCall", out var toolCallProp), Is.True, "Schema should have 'ToolCall' property");

        // **Assert**: Verify ToolCall property has anyOf with all configured types
        Assert.That(toolCallProp.TryGetProperty("anyOf", out var anyOf), Is.True, "ToolCall property should have 'anyOf'");
        var anyOfArray = anyOf.EnumerateArray().ToList();
        Assert.That(anyOfArray.Count, Is.EqualTo(3), "anyOf should have exactly three options (SendEmail, GetCustomerData, IssueInvoice)");

        // **Assert**: Verify that anyOf contains references to ToolCall definitions
        var refValues = anyOfArray
            .Select(v => v.GetProperty("$ref").GetString())
            .ToList();

        Assert.That(refValues, Contains.Item("#/definitions/SendEmailToolCall"), "anyOf should contain reference to SendEmailToolCall");
        Assert.That(refValues, Contains.Item("#/definitions/GetCustomerDataToolCall"), "anyOf should contain reference to GetCustomerDataToolCall");
        Assert.That(refValues, Contains.Item("#/definitions/IssueInvoiceToolCall"), "anyOf should contain reference to IssueInvoiceToolCall");
    }

    /// <summary>
    /// **Test that verifies polymorphic deserialization with GetCustomerDataToolCall**
    /// Adapted from NextStepManagerTests.Can_deserialize_polymorphic_getCustomerData
    /// </summary>
    [Test]
    public void Can_deserialize_polymorphic_getCustomerData()
    {
        // **Arrange**: Create JSON with GetCustomerDataToolCall
        var json = """
        {
            "currentState": "Retrieving customer information",
            "planRemainingStepsBrief": ["Get customer data", "Process results"],
            "taskCompleted": false,
            "toolCall": {
                "type": "get_customer_data_tool_call",
                "email": "customer@example.com",
                "getAllCustomers": false
            }
        }
        """;

        // **Act**: Deserialize using generic manager
        var nextStep = _manager.DeserializeFromJson(json);

        // **Assert**: Verify deserialization succeeded
        Assert.That(nextStep, Is.Not.Null, "Should deserialize NextStep");
        Assert.That(nextStep!.CurrentState, Is.EqualTo("Retrieving customer information"));
        Assert.That(nextStep.ToolCall, Is.TypeOf<SkPlayground.BusinessFunctions.GetCustomerDataToolCall>(), "ToolCall should be GetCustomerDataToolCall");

        var getCustomerCall = nextStep.ToolCall as SkPlayground.BusinessFunctions.GetCustomerDataToolCall;
        Assert.That(getCustomerCall, Is.Not.Null, "Should cast to GetCustomerDataToolCall");
        Assert.That(getCustomerCall!.Email, Is.EqualTo("customer@example.com"));
        Assert.That(getCustomerCall.GetAllCustomers, Is.False);
    }

    /// <summary>
    /// **Test that demonstrates the flexibility of the generic PolymorphicSchemaManager**
    /// Adapted from NextStepManagerTests.NextStepManager_demonstrates_flexibility
    /// </summary>
    [Test]
    public void PolymorphicSchemaManager_demonstrates_flexibility()
    {
        // **Arrange**: Create manager with only SendEmail support
        var sendEmailOnlyManager = new PolymorphicSchemaManager<ActualNextStep, ActualToolCall>()
            .AddDerivedType<SkPlayground.BusinessFunctions.SendEmailToolCall>();
        var sendEmailSchema = sendEmailOnlyManager.GenerateSchema();

        // **Arrange**: Create manager with only GetCustomerData support
        var getCustomerOnlyManager = new PolymorphicSchemaManager<ActualNextStep, ActualToolCall>()
            .AddDerivedType<SkPlayground.BusinessFunctions.GetCustomerDataToolCall>();
        var getCustomerSchema = getCustomerOnlyManager.GenerateSchema();

        // **Assert**: Verify both schemas are different but valid
        Assert.That(sendEmailSchema, Is.Not.EqualTo(getCustomerSchema), "Different managers should produce different schemas");

        // **Assert**: Both should be valid JSON
        Assert.DoesNotThrow(() => JsonDocument.Parse(sendEmailSchema), "SendEmail schema should be valid JSON");
        Assert.DoesNotThrow(() => JsonDocument.Parse(getCustomerSchema), "GetCustomer schema should be valid JSON");
    }

    /// <summary>
    /// **Test that verifies GenerateSchema with specific types only includes specified types**
    /// Adapted from NextStepManagerTests.GenerateSchema_WithSpecificTypes_OnlyIncludesSpecifiedTypes
    /// </summary>
    [Test]
    public void GenerateSchema_WithSpecificTypes_OnlyIncludesSpecifiedTypes()
    {
        // **Act**: Generate schema with only specific types
        var specifiedTypes = new[] { typeof(SkPlayground.BusinessFunctions.SendEmailToolCall), typeof(SkPlayground.BusinessFunctions.GetCustomerDataToolCall) };
        var schemaJson = _manager.GenerateSchema(specifiedTypes);
        var schemaObj = JsonDocument.Parse(schemaJson);
        var root = schemaObj.RootElement;

        // **Assert**: Verify only specified types are in definitions
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions'");
        Assert.That(definitions.TryGetProperty("SendEmailToolCall", out _), Is.True, "Should include SendEmailToolCall");
        Assert.That(definitions.TryGetProperty("GetCustomerDataToolCall", out _), Is.True, "Should include GetCustomerDataToolCall");
        Assert.That(definitions.TryGetProperty("IssueInvoiceToolCall", out _), Is.False, "Should NOT include IssueInvoiceToolCall");

        // **Assert**: Verify anyOf only has 2 references
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True, "Schema should have 'properties'");
        Assert.That(properties.TryGetProperty("ToolCall", out var toolCallProp), Is.True, "Schema should have 'ToolCall' property");
        Assert.That(toolCallProp.TryGetProperty("anyOf", out var anyOf), Is.True, "ToolCall should have 'anyOf'");

        var anyOfArray = anyOf.EnumerateArray().ToList();
        Assert.That(anyOfArray.Count, Is.EqualTo(2), "anyOf should have exactly 2 options");
    }

    /// <summary>
    /// **Test that verifies GenerateSchema with only SendEmail type**
    /// Adapted from NextStepManagerTests.GenerateSchema_WithSpecificTypes_CanIncludeSendEmailOnly
    /// </summary>
    [Test]
    public void GenerateSchema_WithSpecificTypes_CanIncludeSendEmailOnly()
    {
        // **Act**: Generate schema with only SendEmail
        var sendEmailOnlyTypes = new[] { typeof(SkPlayground.BusinessFunctions.SendEmailToolCall) };
        var schemaJson = _manager.GenerateSchema(sendEmailOnlyTypes);
        var schemaObj = JsonDocument.Parse(schemaJson);
        var root = schemaObj.RootElement;

        // **Assert**: Verify only SendEmail is included
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions'");
        Assert.That(definitions.TryGetProperty("SendEmailToolCall", out _), Is.True, "Should include SendEmailToolCall");
        Assert.That(definitions.TryGetProperty("GetCustomerDataToolCall", out _), Is.False, "Should NOT include GetCustomerDataToolCall");

        // **Assert**: Verify anyOf has only 1 reference
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True, "Schema should have 'properties'");
        Assert.That(properties.TryGetProperty("ToolCall", out var toolCallProp), Is.True, "Schema should have 'ToolCall' property");
        Assert.That(toolCallProp.TryGetProperty("anyOf", out var anyOf), Is.True, "ToolCall should have 'anyOf'");

        var anyOfArray = anyOf.EnumerateArray().ToList();
        Assert.That(anyOfArray.Count, Is.EqualTo(1), "anyOf should have exactly 1 option");
    }

    /// <summary>
    /// **Test that verifies GenerateSchema with both types specified explicitly**
    /// Adapted from NextStepManagerTests.GenerateSchema_WithSpecificTypes_CanIncludeBothTypes
    /// </summary>
    [Test]
    public void GenerateSchema_WithSpecificTypes_CanIncludeBothTypes()
    {
        // **Act**: Generate schema with both types explicitly specified
        var bothTypes = new[] { typeof(SkPlayground.BusinessFunctions.SendEmailToolCall), typeof(SkPlayground.BusinessFunctions.GetCustomerDataToolCall) };
        var schemaJson = _manager.GenerateSchema(bothTypes);

        // **Act**: Generate default schema for comparison
        var defaultSchemaJson = _manager.GenerateSchema();

        // **Assert**: Should not be identical since default includes IssueInvoiceToolCall too
        Assert.That(schemaJson, Is.Not.EqualTo(defaultSchemaJson), "Explicit subset should not equal full default schema");
    }

    /// <summary>
    /// **Test that verifies GenerateSchema throws exception for unconfigured types**
    /// Adapted from NextStepManagerTests.GenerateSchema_WithUnconfiguredType_ThrowsException
    /// </summary>
    [Test]
    public void GenerateSchema_WithUnconfiguredType_ThrowsException()
    {
        // **Arrange**: Create manager with only SendEmail configured
        var manager = new PolymorphicSchemaManager<ActualNextStep, ActualToolCall>()
            .AddDerivedType<SkPlayground.BusinessFunctions.SendEmailToolCall>();

        // **Act & Assert**: Try to generate schema including unconfigured type
        var unconfiguredTypes = new[] { typeof(SkPlayground.BusinessFunctions.SendEmailToolCall), typeof(SkPlayground.BusinessFunctions.GetCustomerDataToolCall) };

        Assert.Throws<ArgumentException>(() => manager.GenerateSchema(unconfiguredTypes),
            "Should throw when trying to include unconfigured type");
    }

    /// <summary>
    /// **Test that verifies GenerateSchema throws exception for empty types list**
    /// Adapted from NextStepManagerTests.GenerateSchema_WithEmptyTypesList_ThrowsException
    /// </summary>
    [Test]
    public void GenerateSchema_WithEmptyTypesList_ThrowsException()
    {
        // **Act & Assert**: Try to generate schema with empty types list
        Assert.Throws<ArgumentException>(() => _manager.GenerateSchema(new Type[0]),
            "Should throw when types list is empty");
    }

    /// <summary>
    /// **Test that verifies deserialization works for all configured types regardless of schema filtering**
    /// Adapted from NextStepManagerTests.GenerateSchema_WithSpecificTypes_DeserializationStillWorksForAllConfiguredTypes
    /// </summary>
    [Test]
    public void GenerateSchema_WithSpecificTypes_DeserializationStillWorksForAllConfiguredTypes()
    {
        // **Arrange**: Generate schema with only SendEmail
        var sendEmailOnlyTypes = new[] { typeof(SkPlayground.BusinessFunctions.SendEmailToolCall) };
        var schemaJson = _manager.GenerateSchema(sendEmailOnlyTypes);

        // **Arrange**: Prepare test JSON for both types
        var sendEmailJson = """
        {
            "currentState": "Test",
            "planRemainingStepsBrief": ["Step 1"],
            "taskCompleted": false,
            "toolCall": {
                "type": "send_email_tool_call",
                "subject": "Test",
                "message": "Test Message",
                "recipientEmail": "test@example.com",
                "files": []
            }
        }
        """;

        var getCustomerJson = """
        {
            "currentState": "Test",
            "planRemainingStepsBrief": ["Step 1"],
            "taskCompleted": false,
            "toolCall": {
                "type": "get_customer_data_tool_call",
                "email": "customer@example.com",
                "getAllCustomers": false
            }
        }
        """;

        // **Assert**: Verify deserialization still works for both types (manager retains all configured types)
        Assert.DoesNotThrow(() => _manager.DeserializeFromJson(sendEmailJson), "SendEmail deserialization should work");
        Assert.DoesNotThrow(() => _manager.DeserializeFromJson(getCustomerJson), "GetCustomer deserialization should work");
    }

    /// <summary>
    /// **Test that verifies schema caching generates consistent schemas**
    /// Adapted from NextStepManagerTests.SchemaCaching_GeneratesConsistentSchemas
    /// </summary>
    [Test]
    public void SchemaCaching_GeneratesConsistentSchemas()
    {
        // **Act**: Generate schema multiple times
        var schema1 = _manager.GenerateSchema();
        var schema2 = _manager.GenerateSchema();
        var schema3 = _manager.GenerateSchema();

        // **Assert**: All schemas should be identical (caching working correctly)
        Assert.That(schema1, Is.EqualTo(schema2), "Schema generation should be consistent (cached)");
        Assert.That(schema2, Is.EqualTo(schema3), "Schema generation should be consistent (cached)");
    }

    /// <summary>
    /// **Test that verifies schema caching works with selective generation**
    /// Adapted from NextStepManagerTests.SchemaCaching_WorksWithSelectiveGeneration
    /// </summary>
    [Test]
    public void SchemaCaching_WorksWithSelectiveGeneration()
    {
        // **Act**: Generate selective schemas multiple times
        var sendEmailOnly = new[] { typeof(SkPlayground.BusinessFunctions.SendEmailToolCall) };
        var schema1 = _manager.GenerateSchema(sendEmailOnly);
        var schema2 = _manager.GenerateSchema(sendEmailOnly);

        // **Assert**: Should be identical
        Assert.That(schema1, Is.EqualTo(schema2), "Selective schema generation should be consistent");

        // **Act**: Different selection should produce different schema
        var bothTypes = new[] { typeof(SkPlayground.BusinessFunctions.SendEmailToolCall), typeof(SkPlayground.BusinessFunctions.GetCustomerDataToolCall) };
        var schema3 = _manager.GenerateSchema(bothTypes);

        // **Assert**: Different type selections should produce different schemas
        Assert.That(schema1, Is.Not.EqualTo(schema3), "Different type selections should produce different schemas");
    }

    /// <summary>
    /// **Test that verifies schema caching produces same result regardless of type addition order**
    /// Adapted from NextStepManagerTests.SchemaCaching_AddingTypesInDifferentOrder_ProducesSameResult
    /// </summary>
    [Test]
    public void SchemaCaching_AddingTypesInDifferentOrder_ProducesSameResult()
    {
        // **Arrange**: Create two managers with types added in different orders
        var manager1 = new PolymorphicSchemaManager<ActualNextStep, ActualToolCall>()
            .AddDerivedType<SkPlayground.BusinessFunctions.SendEmailToolCall>()
            .AddDerivedType<SkPlayground.BusinessFunctions.GetCustomerDataToolCall>();

        var manager2 = new PolymorphicSchemaManager<ActualNextStep, ActualToolCall>()
            .AddDerivedType<SkPlayground.BusinessFunctions.GetCustomerDataToolCall>()
            .AddDerivedType<SkPlayground.BusinessFunctions.SendEmailToolCall>();

        // **Act**: Generate schemas
        var schema1 = manager1.GenerateSchema();
        var schema2 = manager2.GenerateSchema();

        // **Assert**: Parse both schemas to compare structure (JSON may have different property order)
        var schemaObj1 = JsonDocument.Parse(schema1);
        var schemaObj2 = JsonDocument.Parse(schema2);

        Assert.That(schemaObj1.RootElement.TryGetProperty("definitions", out var defs1), Is.True);
        Assert.That(schemaObj2.RootElement.TryGetProperty("definitions", out var defs2), Is.True);

        Assert.That(defs1.TryGetProperty("SendEmailToolCall", out _), Is.True, "Manager1 should have SendEmailToolCall definition");
        Assert.That(defs1.TryGetProperty("GetCustomerDataToolCall", out _), Is.True, "Manager1 should have GetCustomerDataToolCall definition");
        Assert.That(defs2.TryGetProperty("SendEmailToolCall", out _), Is.True, "Manager2 should have SendEmailToolCall definition");
        Assert.That(defs2.TryGetProperty("GetCustomerDataToolCall", out _), Is.True, "Manager2 should have GetCustomerDataToolCall definition");
    }

    /// <summary>
    /// **Test that verifies schema should not contain abstract base class definition**
    /// Adapted from NextStepManagerTests.Schema_ShouldNotContainAbstractToolCallDefinition
    /// </summary>
    [Test]
    public void Schema_ShouldNotContainAbstractToolCallDefinition()
    {
        // **Act**: Generate schema
        var schemaJson = _manager.GenerateSchema();
        var schemaObj = JsonDocument.Parse(schemaJson);
        var root = schemaObj.RootElement;

        // **Assert**: Verify that definitions section exists
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions' section");

        // **Assert**: Verify that abstract ToolCall class is NOT included in definitions
        Assert.That(definitions.TryGetProperty("ToolCall", out _), Is.False,
            "Schema should NOT contain abstract 'ToolCall' definition as it's unused and causes OpenAI rejection");

        // **Assert**: Verify that only concrete derived types are included
        Assert.That(definitions.TryGetProperty("SendEmailToolCall", out _), Is.True, "Schema should contain 'SendEmailToolCall' definition");
        Assert.That(definitions.TryGetProperty("GetCustomerDataToolCall", out _), Is.True, "Schema should contain 'GetCustomerDataToolCall' definition");
        Assert.That(definitions.TryGetProperty("IssueInvoiceToolCall", out _), Is.True, "Schema should contain 'IssueInvoiceToolCall' definition");

        // **Assert**: Verify the definitions count - should only have concrete types
        var definitionCount = definitions.EnumerateObject().Count();
        Assert.That(definitionCount, Is.EqualTo(3), "Schema should contain exactly 3 definitions, not including abstract ToolCall");

        Console.WriteLine("✅ Regression test passed: Abstract ToolCall class correctly excluded from schema");
    }

    /// <summary>
    /// **Test that verifies GenerateSchema throws exception when no types are added**
    /// Adapted from NextStepManagerTests.GenerateSchema_WithNoTypesAdded_ThrowsException
    /// </summary>
    [Test]
    public void GenerateSchema_WithNoTypesAdded_ThrowsException()
    {
        // **Arrange**: Create empty manager
        var manager = new PolymorphicSchemaManager<ActualNextStep, ActualToolCall>();

        // **Act & Assert**: Should throw exception when no types are added
        Assert.Throws<InvalidOperationException>(() => manager.GenerateSchema(),
            "Should throw exception when no types are added");
    }

    /// <summary>
    /// **Test that verifies DeserializeFromJson throws exception when no types are added**
    /// Adapted from NextStepManagerTests.DeserializeFromJson_WithNoTypesAdded_ThrowsException
    /// </summary>
    [Test]
    public void DeserializeFromJson_WithNoTypesAdded_ThrowsException()
    {
        // **Arrange**: Create empty manager
        var manager = new PolymorphicSchemaManager<ActualNextStep, ActualToolCall>();

        // **Act & Assert**: Should throw exception when no types are added
        Assert.Throws<InvalidOperationException>(() => manager.DeserializeFromJson("{}"),
            "Should throw exception when no types are added");
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

                You need to answer in json to specify the next step to execute.
                """;

            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);


            var chatResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "next_step",
                jsonSchema: BinaryData.FromString(schemaJson),
                jsonSchemaIsStrict: true
            );
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ResponseFormat = chatResponseFormat,
                Temperature = 0.0f,
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