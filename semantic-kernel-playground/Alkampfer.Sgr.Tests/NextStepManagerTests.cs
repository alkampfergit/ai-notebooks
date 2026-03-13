using NUnit.Framework;
using System.Text.Json;
using Alkampfer.Sgr.Utils;
using Alkampfer.Sgr.Playground.Utils;
using Alkampfer.Sgr.Models;
using Alkampfer.Sgr.Playground.BusinessFunctions;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;

namespace Alkampfer.Sgr.Tests;

/// <summary>
/// Tests for NextStepManager polymorphic schema generation and deserialization functionality
/// Comprehensive test suite ported from PetOwnerManager tests to ensure feature parity
/// </summary>
[TestFixture]
public class NextStepManagerTests : SemanticKernelTestBase
{
    /// <summary>
    /// Helper method to generate NextStep schema with all ToolCall types
    /// </summary>
    private static string GenerateCompleteNextStepSchema()
    {
        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>()
            .AddDerivedType<IssueInvoiceToolCall>()
            .AddDerivedType<VoidInvoiceToolCall>()
            .AddDerivedType<CreateRuleToolCall>()
            .AddDerivedType<ReportTaskCompletionToolCall>();
        
        return manager.GenerateSchema();
    }

    [Test]
    public void Contains_derived_references()
    {
        var schema = GenerateCompleteNextStepSchema();
        var schemaObj = JsonDocument.Parse(schema);
        var root = schemaObj.RootElement;
        
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions'");
        Assert.That(definitions.TryGetProperty("SendEmailToolCall", out var sendEmail), Is.True, "Schema should have 'SendEmailToolCall' definition");
        Assert.That(definitions.TryGetProperty("GetCustomerDataToolCall", out var getCustomer), Is.True, "Schema should have 'GetCustomerDataToolCall' definition");

        // Verify SendEmailToolCall schema is correct
        Assert.That(sendEmail.TryGetProperty("properties", out var sendEmailProps), Is.True, "SendEmailToolCall should have 'properties'");
        var sendEmailPropsObj = sendEmailProps.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        Assert.That(sendEmailPropsObj.ContainsKey("type"), Is.True, "SendEmailToolCall should have 'type' property");
        Assert.That(sendEmailPropsObj.ContainsKey("Subject"), Is.True, "SendEmailToolCall should have 'Subject' property");
        Assert.That(sendEmailPropsObj.ContainsKey("Message"), Is.True, "SendEmailToolCall should have 'Message' property");
        Assert.That(sendEmailPropsObj.ContainsKey("RecipientEmail"), Is.True, "SendEmailToolCall should have 'RecipientEmail' property");
    }

    [Test]
    public void Can_generate_schema_with_only_sendEmail()
    {
        // Test the flexibility of the NextStepManager - only SendEmail, no other ToolCalls
        var manager = new NextStepManager().AddDerivedType<SendEmailToolCall>();
        var schemaJson = manager.GenerateSchema();
        var schemaObj = JsonDocument.Parse(schemaJson);
        var root = schemaObj.RootElement;
        
        // Verify schema structure
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions'");
        Assert.That(definitions.TryGetProperty("SendEmailToolCall", out _), Is.True, "Schema should have 'SendEmailToolCall' definition");
        Assert.That(definitions.TryGetProperty("GetCustomerDataToolCall", out _), Is.False, "Schema should NOT have 'GetCustomerDataToolCall' definition");
        
        // Verify NextStepToolToCall property has anyOf with only SendEmail
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True, "Schema should have 'properties'");
        Assert.That(properties.TryGetProperty("Function", out var toolCallProp), Is.True, "Schema should have 'NextStepToolToCall' property");
        Assert.That(toolCallProp.TryGetProperty("anyOf", out var anyOf), Is.True, "NextStepToolToCall property should have 'anyOf'");
        
        var anyOfArray = anyOf.EnumerateArray().ToList();
        Assert.That(anyOfArray.Count, Is.EqualTo(1), "anyOf should have exactly one option (SendEmailToolCall)");
    }

    [Test]
    public void Derived_class_schema_has_discriminator_type()
    {
        var schema = GenerateCompleteNextStepSchema();
        var schemaObj = JsonDocument.Parse(schema);
        var root = schemaObj.RootElement;
        
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions'");
        Assert.That(definitions.TryGetProperty("SendEmailToolCall", out var sendEmail), Is.True, "Schema should have 'SendEmailToolCall' definition");

        // Verify SendEmailToolCall has type property with enum "send_email_tool_call"
        Assert.That(sendEmail.TryGetProperty("properties", out var sendEmailProps), Is.True, "SendEmailToolCall should have 'properties'");
        var sendEmailPropsObj = sendEmailProps.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        Assert.That(sendEmailPropsObj.ContainsKey("type"), Is.True, "SendEmailToolCall should have 'type' property");
        var typeProp = sendEmailPropsObj["type"];

        Assert.That(typeProp.TryGetProperty("enum", out var enumValues), Is.True, "type property should have 'enum'");
        var enums = enumValues.EnumerateArray().Select(v => v.GetString()).ToList();
        Assert.That(enums, Contains.Item("send_email_tool_call"), "type enum should contain 'send_email_tool_call'");

        // Should have a title
        Assert.That(typeProp.TryGetProperty("title", out var titleValue), Is.True, "type property should have 'title'");
        Assert.That(titleValue.GetString(), Is.EqualTo("Type"), "type property should have title 'Type'");

        // Type of the property should be string
        Assert.That(typeProp.TryGetProperty("type", out var typeValue), Is.True, "type property should have 'type'");
        Assert.That(typeValue.GetString(), Is.EqualTo("string"), "type property should be of type 'string'");

        // Should have const value
        Assert.That(typeProp.TryGetProperty("const", out var constValue), Is.True, "type property should have 'const'");
        Assert.That(constValue.GetString(), Is.EqualTo("send_email_tool_call"), "const should be 'send_email_tool_call'");
    }

    [Test]
    public void Base_class_Contains_anyof()
    {
        var schema = GenerateCompleteNextStepSchema();
        var schemaObj = JsonDocument.Parse(schema);
        var root = schemaObj.RootElement;
        
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True, "Schema should have 'properties'");
        Assert.That(properties.TryGetProperty("Function", out var toolCallProp), Is.True, "Schema should have 'NextStepToolToCall' property");

        // Verify NextStepToolToCall property has anyOf with SendEmail and GetCustomerData
        Assert.That(toolCallProp.TryGetProperty("anyOf", out var anyOf), Is.True, "Function property should have 'anyOf'");
        var anyOfArray = anyOf.EnumerateArray().ToList();
        Assert.That(anyOfArray.Count, Is.EqualTo(6), "anyOf should have exactly six options (all ToolCall types)");

        // Verify that anyOf contains references to ToolCall definitions
        var refValues = anyOfArray
            .Select(v => v.GetProperty("$ref").GetString())
            .ToList();

        Assert.That(refValues, Contains.Item("#/definitions/SendEmailToolCall"), "anyOf should contain reference to SendEmailToolCall");
        Assert.That(refValues, Contains.Item("#/definitions/GetCustomerDataToolCall"), "anyOf should contain reference to GetCustomerDataToolCall");
        Assert.That(refValues, Contains.Item("#/definitions/IssueInvoiceToolCall"), "anyOf should contain reference to IssueInvoiceToolCall");
    }

    [Test]
    public void Can_deserialize_polymorphic()
    {
        var json = """
        {
            "CurrentState": "Ready to send email",
            "PlanRemainingStepsBrief": ["Send email", "Confirm delivery"],
            "TaskCompleted": false,
            "Function": {
                "type": "send_email_tool_call",
                "Subject": "Important Update",
                "Message": "Please review the latest changes",
                "RecipientEmail": "user@company.com",
                "Files": ["report.pdf", "summary.docx"]
            }
        }
        """;

        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>();

        var nextStep = manager.DeserializeFromJson(json);

        Assert.That(nextStep, Is.Not.Null, "Should deserialize NextStep");
        Assert.That(nextStep.CurrentState, Is.EqualTo("Ready to send email"));
        Assert.That(nextStep.TaskCompleted, Is.False);
        Assert.That(nextStep.PlanRemainingStepsBrief.Count, Is.EqualTo(2));
        Assert.That(nextStep.Function, Is.Not.Null, "ToolCall should not be null");
        Assert.That(nextStep.Function, Is.TypeOf<SendEmailToolCall>(), "ToolCall should be SendEmailToolCall");

        var sendEmailCall = nextStep.Function as SendEmailToolCall;
        Assert.That(sendEmailCall, Is.Not.Null, "Should cast to SendEmailToolCall");
        Assert.That(sendEmailCall.Subject, Is.EqualTo("Important Update"));
        Assert.That(sendEmailCall.Message, Is.EqualTo("Please review the latest changes"));
        Assert.That(sendEmailCall.RecipientEmail, Is.EqualTo("user@company.com"));
        Assert.That(sendEmailCall.Files.Count, Is.EqualTo(2));
        Assert.That(sendEmailCall.Files, Contains.Item("report.pdf"));
    }

    [Test]
    public void Can_deserialize_polymorphic_getCustomerData()
    {
        var json = """
        {
            "CurrentState": "Retrieving customer information",
            "PlanRemainingStepsBrief": ["Get customer data", "Process results"],
            "TaskCompleted": false,
            "Function": {
                "type": "get_customer_data_tool_call",
                "Email": "customer@example.com"
            }
        }
        """;

        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>();

        var nextStep = manager.DeserializeFromJson(json);

        Assert.That(nextStep, Is.Not.Null, "Should deserialize NextStep");
        Assert.That(nextStep.CurrentState, Is.EqualTo("Retrieving customer information"));
        Assert.That(nextStep.Function, Is.TypeOf<GetCustomerDataToolCall>(), "ToolCall should be GetCustomerDataToolCall");

        var getCustomerCall = nextStep.Function as GetCustomerDataToolCall;
        Assert.That(getCustomerCall, Is.Not.Null, "Should cast to GetCustomerDataToolCall");
        Assert.That(getCustomerCall.Email, Is.EqualTo("customer@example.com"));
    }

    [Test]
    public void NextStepManager_demonstrates_flexibility()
    {
        // Create manager with only SendEmail support
        var sendEmailOnlyManager = new NextStepManager().AddDerivedType<SendEmailToolCall>();
        var sendEmailSchema = sendEmailOnlyManager.GenerateSchema();
        
        // Create manager with only GetCustomerData support
        var getCustomerOnlyManager = new NextStepManager().AddDerivedType<GetCustomerDataToolCall>();
        var getCustomerSchema = getCustomerOnlyManager.GenerateSchema();
        
        // Verify both schemas are different but valid
        Assert.That(sendEmailSchema, Is.Not.EqualTo(getCustomerSchema), "Different managers should produce different schemas");
        
        // Both should be valid JSON
        Assert.DoesNotThrow(() => JsonDocument.Parse(sendEmailSchema), "SendEmail schema should be valid JSON");
        Assert.DoesNotThrow(() => JsonDocument.Parse(getCustomerSchema), "GetCustomer schema should be valid JSON");
    }

    [Test]
    public void GenerateSchema_WithSpecificTypes_OnlyIncludesSpecifiedTypes()
    {
        // Create manager with multiple ToolCall types configured
        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>()
            .AddDerivedType<IssueInvoiceToolCall>();

        // Generate schema with only specific types
        var specifiedTypes = new[] { typeof(SendEmailToolCall), typeof(GetCustomerDataToolCall) };
        var schemaJson = manager.GenerateSchema(specifiedTypes);
        var schemaObj = JsonDocument.Parse(schemaJson);
        var root = schemaObj.RootElement;

        // Verify only specified types are in definitions
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions'");
        Assert.That(definitions.TryGetProperty("SendEmailToolCall", out _), Is.True, "Should include SendEmailToolCall");
        Assert.That(definitions.TryGetProperty("GetCustomerDataToolCall", out _), Is.True, "Should include GetCustomerDataToolCall");
        Assert.That(definitions.TryGetProperty("IssueInvoiceToolCall", out _), Is.False, "Should NOT include IssueInvoiceToolCall");

        // Verify anyOf only has 2 references
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True, "Schema should have 'properties'");
        Assert.That(properties.TryGetProperty("Function", out var toolCallProp), Is.True, "Schema should have 'NextStepToolToCall' property");
        Assert.That(toolCallProp.TryGetProperty("anyOf", out var anyOf), Is.True, "NextStepToolToCall should have 'anyOf'");
        
        var anyOfArray = anyOf.EnumerateArray().ToList();
        Assert.That(anyOfArray.Count, Is.EqualTo(2), "anyOf should have exactly 2 options");
    }

    [Test]
    public void GenerateSchema_WithSpecificTypes_CanIncludeSendEmailOnly()
    {
        // Create manager with multiple types configured
        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>();

        // Generate schema with only SendEmail
        var sendEmailOnlyTypes = new[] { typeof(SendEmailToolCall) };
        var schemaJson = manager.GenerateSchema(sendEmailOnlyTypes);
        var schemaObj = JsonDocument.Parse(schemaJson);
        var root = schemaObj.RootElement;

        // Verify only SendEmail is included
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions'");
        Assert.That(definitions.TryGetProperty("SendEmailToolCall", out _), Is.True, "Should include SendEmailToolCall");
        Assert.That(definitions.TryGetProperty("GetCustomerDataToolCall", out _), Is.False, "Should NOT include GetCustomerDataToolCall");

        // Verify anyOf has only 1 reference
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True, "Schema should have 'properties'");
        Assert.That(properties.TryGetProperty("Function", out var toolCallProp), Is.True, "Schema should have 'NextStepToolToCall' property");
        Assert.That(toolCallProp.TryGetProperty("anyOf", out var anyOf), Is.True, "NextStepToolToCall should have 'anyOf'");
        
        var anyOfArray = anyOf.EnumerateArray().ToList();
        Assert.That(anyOfArray.Count, Is.EqualTo(1), "anyOf should have exactly 1 option");
    }

    [Test]
    public void GenerateSchema_WithSpecificTypes_CanIncludeBothTypes()
    {
        // Create manager with both types configured
        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>();

        // Generate schema with both types explicitly specified
        var bothTypes = new[] { typeof(SendEmailToolCall), typeof(GetCustomerDataToolCall) };
        var schemaJson = manager.GenerateSchema(bothTypes);
        var schemaObj = JsonDocument.Parse(schemaJson);
        var root = schemaObj.RootElement;

        // Should be identical to calling GenerateSchema() without parameters
        var defaultSchemaJson = manager.GenerateSchema();
        Assert.That(schemaJson, Is.EqualTo(defaultSchemaJson), "Explicit all-types should equal default schema");
    }

    [Test]
    public void GenerateSchema_WithUnconfiguredType_ThrowsException()
    {
        // Create manager with only SendEmail configured
        var manager = new NextStepManager().AddDerivedType<SendEmailToolCall>();

        // Try to generate schema including unconfigured type
        var unconfiguredTypes = new[] { typeof(SendEmailToolCall), typeof(GetCustomerDataToolCall) };
        
        Assert.Throws<ArgumentException>(() => manager.GenerateSchema(unconfiguredTypes), 
            "Should throw when trying to include unconfigured type");
    }

    [Test]
    public void GenerateSchema_WithEmptyTypesList_ThrowsException()
    {
        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>();

        Assert.Throws<ArgumentException>(() => manager.GenerateSchema(new Type[0]), 
            "Should throw when types list is empty");
    }

    [Test]
    public void GenerateSchema_WithSpecificTypes_DeserializationStillWorksForAllConfiguredTypes()
    {
        // Create manager with both types configured
        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>();

        // Generate schema with only SendEmail
        var sendEmailOnlyTypes = new[] { typeof(SendEmailToolCall) };
        var schemaJson = manager.GenerateSchema(sendEmailOnlyTypes);

        // Verify deserialization still works for both types (manager retains all configured types)
        var sendEmailJson = """
        {
            "CurrentState": "Test",
            "PlanRemainingStepsBrief": ["Step 1"],
            "TaskCompleted": false,
            "Function": {
                "type": "send_email_tool_call",
                "Subject": "Test",
                "Message": "Test Message",
                "RecipientEmail": "test@example.com",
                "Files": []
            }
        }
        """;

        var getCustomerJson = """
        {
            "CurrentState": "Test",
            "PlanRemainingStepsBrief": ["Step 1"],
            "TaskCompleted": false,
            "Function": {
                "type": "get_customer_data_tool_call",
                "Email": "customer@example.com"
            }
        }
        """;

        Assert.DoesNotThrow(() => manager.DeserializeFromJson(sendEmailJson), "SendEmail deserialization should work");
        Assert.DoesNotThrow(() => manager.DeserializeFromJson(getCustomerJson), "GetCustomer deserialization should work");
    }

    [Test]
    public void SchemaCaching_GeneratesConsistentSchemas()
    {
        // Create manager and add types
        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>();

        // Generate schema multiple times
        var schema1 = manager.GenerateSchema();
        var schema2 = manager.GenerateSchema();
        var schema3 = manager.GenerateSchema();

        // All schemas should be identical (caching working correctly)
        Assert.That(schema1, Is.EqualTo(schema2), "Schema generation should be consistent (cached)");
        Assert.That(schema2, Is.EqualTo(schema3), "Schema generation should be consistent (cached)");
    }

    [Test]
    public void SchemaCaching_WorksWithSelectiveGeneration()
    {
        // Create manager and add types
        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>()
            .AddDerivedType<IssueInvoiceToolCall>();

        // Generate selective schemas multiple times
        var sendEmailOnly = new[] { typeof(SendEmailToolCall) };
        var schema1 = manager.GenerateSchema(sendEmailOnly);
        var schema2 = manager.GenerateSchema(sendEmailOnly);

        // Should be identical
        Assert.That(schema1, Is.EqualTo(schema2), "Selective schema generation should be consistent");

        // Different selection should produce different schema
        var bothTypes = new[] { typeof(SendEmailToolCall), typeof(GetCustomerDataToolCall) };
        var schema3 = manager.GenerateSchema(bothTypes);
        Assert.That(schema1, Is.Not.EqualTo(schema3), "Different type selections should produce different schemas");
    }

    [Test]
    public void SchemaCaching_AddingTypesInDifferentOrder_ProducesSameResult()
    {
        // Create two managers with types added in different orders
        var manager1 = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>();

        var manager2 = new NextStepManager()
            .AddDerivedType<GetCustomerDataToolCall>()
            .AddDerivedType<SendEmailToolCall>();

        var schema1 = manager1.GenerateSchema();
        var schema2 = manager2.GenerateSchema();

        // Parse both schemas to compare structure (JSON may have different property order)
        var schemaObj1 = JsonDocument.Parse(schema1);
        var schemaObj2 = JsonDocument.Parse(schema2);

        Assert.That(schemaObj1.RootElement.TryGetProperty("definitions", out var defs1), Is.True);
        Assert.That(schemaObj2.RootElement.TryGetProperty("definitions", out var defs2), Is.True);
        
        Assert.That(defs1.TryGetProperty("SendEmailToolCall", out _), Is.True, "Manager1 should have SendEmailToolCall definition");
        Assert.That(defs1.TryGetProperty("GetCustomerDataToolCall", out _), Is.True, "Manager1 should have GetCustomerDataToolCall definition");
        Assert.That(defs2.TryGetProperty("SendEmailToolCall", out _), Is.True, "Manager2 should have SendEmailToolCall definition");
        Assert.That(defs2.TryGetProperty("GetCustomerDataToolCall", out _), Is.True, "Manager2 should have GetCustomerDataToolCall definition");
    }

    [Test]
    public void Schema_ShouldNotContainAbstractToolCallDefinition()
    {
        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>();
        
        var schemaJson = manager.GenerateSchema();
        var schemaObj = JsonDocument.Parse(schemaJson);
        var root = schemaObj.RootElement;
        
        // Verify that definitions section exists
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions' section");
        
        // Verify that abstract ToolCall class is NOT included in definitions
        Assert.That(definitions.TryGetProperty("ToolCall", out _), Is.False, 
            "Schema should NOT contain abstract 'ToolCall' definition as it's unused and causes OpenAI rejection");
        
        // Verify that only concrete derived types are included
        Assert.That(definitions.TryGetProperty("SendEmailToolCall", out _), Is.True, "Schema should contain 'SendEmailToolCall' definition");
        Assert.That(definitions.TryGetProperty("GetCustomerDataToolCall", out _), Is.True, "Schema should contain 'GetCustomerDataToolCall' definition");
        
        // Verify the definitions count - should only have concrete types
        var definitionCount = definitions.EnumerateObject().Count();
        Assert.That(definitionCount, Is.EqualTo(2), "Schema should contain exactly 2 definitions, not including abstract ToolCall");
        
        Console.WriteLine("✅ Regression test passed: Abstract ToolCall class correctly excluded from schema");
    }
    
    [Test]
    public void GetDiscriminatorValue_ConvertsCorrectlyToSnakeCase()
    {
        var manager = new NextStepManager();
        
        Assert.That(manager.GetDiscriminatorValue(typeof(SendEmailToolCall)), Is.EqualTo("send_email_tool_call"));
        Assert.That(manager.GetDiscriminatorValue(typeof(GetCustomerDataToolCall)), Is.EqualTo("get_customer_data_tool_call"));
        Assert.That(manager.GetDiscriminatorValue(typeof(IssueInvoiceToolCall)), Is.EqualTo("issue_invoice_tool_call"));
        Assert.That(manager.GetDiscriminatorValue(typeof(VoidInvoiceToolCall)), Is.EqualTo("void_invoice_tool_call"));
        Assert.That(manager.GetDiscriminatorValue(typeof(CreateRuleToolCall)), Is.EqualTo("create_rule_tool_call"));
        Assert.That(manager.GetDiscriminatorValue(typeof(ReportTaskCompletionToolCall)), Is.EqualTo("report_task_completion_tool_call"));
    }
    
    [Test]
    public void GenerateSchema_WithNoTypesAdded_ThrowsException()
    {
        var manager = new NextStepManager();
        
        Assert.Throws<InvalidOperationException>(() => manager.GenerateSchema(), 
            "Should throw exception when no types are added");
    }
    
    [Test]
    public void DeserializeFromJson_WithNoTypesAdded_ThrowsException()
    {
        var manager = new NextStepManager();
        
        Assert.Throws<InvalidOperationException>(() => manager.DeserializeFromJson("{}"), 
            "Should throw exception when no types are added");
    }
    
    /// <summary>
    /// Real LLM test that validates NextStep schema generation and polymorphic deserialization with OpenAI
    /// This test mimics the GenerateJsonSchema_RealLLMCall_PolymorphicCatOwner test for NextStep domain
    /// </summary>
    [Test]
    [Category("LLMIntegration")]
    public async Task GenerateNextStepSchema_RealLLMCall_PolymorphicSendEmailToolCall()
    {
        // Skip test if no API key is available
        var apiKey = Dotenv.Get("OPENAI_API_KEY");
        var endpoint = Dotenv.Get("AZURE_ENDPOINT");

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint))
        {
            Assert.Ignore("OPENAI_API_KEY or AZURE_ENDPOINT environment variable not set");
            return;
        }

        // Generate the JSON schema for NextStep class using polymorphic schema generation
        var schemaJson = GeneratePolymorphicNextStepJsonSchema();
        IChatCompletionService chatService = GetCompletionService(apiKey, endpoint, schemaJson);

        var userPrompt = @"Please format this workflow step data into JSON:
    Current workflow state: 'Ready to send notification email'
    Remaining steps: ['Send email to customer', 'Wait for confirmation', 'Update status']
    Task is not yet completed.
    Next action: Send an email with subject 'Order Confirmation' and message 'Your order #12345 has been processed successfully' to customer@example.com with attached receipt.pdf";

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(userPrompt);

        var chatResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "next_step",
            jsonSchema: BinaryData.FromString(schemaJson),
            jsonSchemaIsStrict: true
        );
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = chatResponseFormat
        };

        Console.WriteLine("\nCalling LLM to reformat NextStep workflow data...");
        Console.WriteLine($"Generated Schema:\n{schemaJson}\n");

        // Test deserialization into our NextStep class with polymorphic ToolCall
        try
        {
            // Make the LLM call
            var skResponse = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings);
            var jsonResponse = skResponse.Content;

            Console.WriteLine("\nLLM Response:");
            Console.WriteLine(jsonResponse);

            var manager = new NextStepManager()
                .AddDerivedType<SendEmailToolCall>()
                .AddDerivedType<GetCustomerDataToolCall>()
                .AddDerivedType<IssueInvoiceToolCall>();
            var nextStep = manager.DeserializeFromJson(jsonResponse);

            Assert.That(nextStep, Is.Not.Null, "Should deserialize to NextStep object");
            Assert.That(nextStep.Function, Is.Not.Null, "ToolCall should not be null");

            // Verify polymorphic deserialization - should be a SendEmailToolCall
            Assert.That(nextStep.Function, Is.TypeOf<SendEmailToolCall>(), "ToolCall should deserialize as SendEmailToolCall type");

            var sendEmailCall = nextStep.Function as SendEmailToolCall;
            Assert.That(sendEmailCall, Is.Not.Null, "Should be able to cast ToolCall to SendEmailToolCall");
            Assert.That(sendEmailCall.Subject, Is.Not.Null.And.Not.Empty, "Email subject should not be empty");
            Assert.That(sendEmailCall.Message, Is.Not.Null.And.Not.Empty, "Email message should not be empty");
            Assert.That(sendEmailCall.RecipientEmail, Is.Not.Null.And.Not.Empty, "Recipient email should not be empty");
            
            Assert.That(sendEmailCall.Subject.ToLower(), Contains.Substring("confirmation").Or.Contains("order"), "Should extract order confirmation subject");
            Assert.That(sendEmailCall.RecipientEmail.ToLower(), Contains.Substring("customer@example.com"), "Should extract recipient email");

            Console.WriteLine($"✅ Successfully extracted NextStep: '{nextStep.CurrentState}' with SendEmailToolCall");
            Console.WriteLine($"✅ Email details: '{sendEmailCall.Subject}' to '{sendEmailCall.RecipientEmail}'");
            Console.WriteLine("✅ Polymorphic deserialization successful - ToolCall correctly identified as SendEmailToolCall");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Failed to perform LLM call: {ex.Message} - Schema: {schemaJson}");
        }

        Console.WriteLine("✅ Real LLM call with polymorphic NextStep schema validation completed successfully!");
    }
    
    /// <summary>
    /// Generates polymorphic JSON schema for NextStep using the NextStepManager
    /// </summary>
    /// <returns>JSON schema string with proper polymorphic ToolCall support</returns>
    private static string GeneratePolymorphicNextStepJsonSchema()
    {
        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>()
            .AddDerivedType<IssueInvoiceToolCall>();
        
        return manager.GenerateSchema();
    }
    
}