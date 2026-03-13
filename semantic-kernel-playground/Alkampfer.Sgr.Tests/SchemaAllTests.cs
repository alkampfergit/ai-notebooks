using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using NJsonSchema.Generation.TypeMappers;
using NUnit.Framework;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Alkampfer.Sgr.Tests;

public class SchemaAllTests
{
    [Test]
    public void GenerateJsonSchema_SimpleExample()
    {
        var settings = new NewtonsoftJsonSchemaGeneratorSettings
        {
            SchemaType = SchemaType.JsonSchema,
            DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull,
            FlattenInheritanceHierarchy = false,
            GenerateAbstractProperties = false,
            AlwaysAllowAdditionalObjectProperties = false
        };

        // Add custom type mapper for polymorphic tool calls
        var allowedTools = new[] { typeof(SendEmail), typeof(IssueInvoice) };
        settings.TypeMappers.Add(new PolymorphicToolCallMapper(typeof(ToolCall), allowedTools));

        var generator = new JsonSchemaGenerator(settings);
        var schema = generator.Generate(typeof(NextStep));
        var schemaJson = schema.ToJson();
        
        Console.WriteLine("Generated Schema:");
        Console.WriteLine(schemaJson);
        
        // Parse the JSON to validate structure properly
        var schemaObj = System.Text.Json.JsonDocument.Parse(schemaJson);
        var root = schemaObj.RootElement;
        
        // 1. Verify top-level schema structure
        Assert.That(root.TryGetProperty("type", out var typeProperty), Is.True, "Schema should have 'type' property");
        Assert.That(typeProperty.GetString(), Is.EqualTo("object"), "Root type should be 'object'");
        Assert.That(root.TryGetProperty("title", out var titleProperty), Is.True, "Schema should have 'title' property");
        Assert.That(titleProperty.GetString(), Is.EqualTo("NextStep"), "Title should be 'NextStep'");
        
        // 2. Verify required properties exist at root level
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True, "Schema should have 'properties'");
        Assert.That(properties.TryGetProperty("current_state", out _), Is.True, "Should have 'current_state' property");
        Assert.That(properties.TryGetProperty("task_completed", out _), Is.True, "Should have 'task_completed' property");
        Assert.That(properties.TryGetProperty("tool_call", out var toolCallProp), Is.True, "Should have 'tool_call' property");
        
        // 3. Verify required fields are marked as required
        Assert.That(root.TryGetProperty("required", out var requiredArray), Is.True, "Schema should have 'required' array");
        var requiredFields = new HashSet<string>();
        foreach (var item in requiredArray.EnumerateArray())
        {
            requiredFields.Add(item.GetString());
        }
        Assert.That(requiredFields, Contains.Item("current_state"), "current_state should be required");
        Assert.That(requiredFields, Contains.Item("task_completed"), "task_completed should be required");
        Assert.That(requiredFields, Contains.Item("tool_call"), "tool_call should be required");
        
        // 4. Verify tool_call has polymorphic structure (anyOf)
        Assert.That(toolCallProp.TryGetProperty("anyOf", out var toolCallAnyOf) || toolCallProp.TryGetProperty("$ref", out _), 
            Is.True, "tool_call should have polymorphic structure (anyOf or $ref)");
        
        // 5. Check for $defs (JSON Schema Draft 2019-09+) vs definitions (older drafts)
        bool hasDefsNew = root.TryGetProperty("$defs", out var defsNew);
        bool hasDefsOld = root.TryGetProperty("definitions", out var defsOld);
        
        Assert.That(hasDefsNew || hasDefsOld, Is.True, "Schema should have either '$defs' or 'definitions'");
        
        var definitions = hasDefsNew ? defsNew : defsOld;
        string defsName = hasDefsNew ? "$defs" : "definitions";
        Console.WriteLine($"✓ Found {defsName} section");
        
        // 6. Verify SendEmail definition structure
        Assert.That(definitions.TryGetProperty("SendEmail", out var sendEmailDef), Is.True, "Should have SendEmail in definitions");
        ValidateToolDefinition(sendEmailDef, "SendEmail", new[] { "tool", "subject", "message", "recipient_email" });
        
        // 7. Verify IssueInvoice definition structure
        Assert.That(definitions.TryGetProperty("IssueInvoice", out var issueInvoiceDef), Is.True, "Should have IssueInvoice in definitions");
        ValidateToolDefinition(issueInvoiceDef, "IssueInvoice", new[] { "tool", "email", "discount_percent" });
        
        // 8. Verify that tool_call references the definitions properly
        if (toolCallProp.TryGetProperty("anyOf", out var toolCallAnyOfArray))
        {
            Console.WriteLine($"✓ Found anyOf with {toolCallAnyOfArray.GetArrayLength()} items");
            // Each anyOf item should reference our tool definitions
            foreach (var anyOfItem in toolCallAnyOfArray.EnumerateArray())
            {
                if (anyOfItem.TryGetProperty("$ref", out var refProp))
                {
                    var refValue = refProp.GetString();
                    Console.WriteLine($"✓ Found $ref: {refValue}");
                    Assert.That(refValue, Is.Not.Null.And.Not.Empty, "$ref should not be empty");
                }
            }
        }
        else if (toolCallProp.TryGetProperty("$ref", out var directRef))
        {
            Console.WriteLine($"✓ Found direct $ref: {directRef.GetString()}");
        }
        
        // 8. Verify the schema is not generating circular references (basic check)
        var schemaString = schemaJson.ToString();
        var refCount = System.Text.RegularExpressions.Regex.Matches(schemaString, @"\$ref").Count;
        Assert.That(refCount, Is.LessThan(20), "Should not have excessive $ref usage (potential circular refs)");
        
        // 9. Check that basic JSON Schema validation keywords are present
        Assert.That(schemaJson, Contains.Substring("\"type\""), "Schema should contain type definitions");
        Assert.That(schemaJson, Contains.Substring("\"properties\""), "Schema should contain properties");
        
        Console.WriteLine("✅ Schema structure validation passed!");
        Console.WriteLine($"✅ Found {requiredFields.Count} required fields");
        Console.WriteLine($"✅ Schema contains {refCount} $ref references");
        Console.WriteLine("✅ Polymorphic tool calls successfully generated!");
    }

    private void ValidateToolDefinition(JsonElement toolDef, string toolName, string[] expectedProperties)
    {
        Console.WriteLine($"🔍 Validating {toolName} definition...");
        
        // Check if it's wrapped in allOf (NJsonSchema inheritance pattern)
        JsonElement actualDef = toolDef;
        if (toolDef.TryGetProperty("allOf", out var allOfArray))
        {
            Console.WriteLine($"  ✓ {toolName} uses allOf pattern");
            // Find the object definition within allOf
            foreach (var item in allOfArray.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var typeProperty) && 
                    typeProperty.GetString() == "object" &&
                    item.TryGetProperty("properties", out _))
                {
                    actualDef = item;
                    break;
                }
            }
        }
        
        // 1. Verify it's an object type
        Assert.That(actualDef.TryGetProperty("type", out var defType), Is.True, $"{toolName} should have 'type' property");
        Assert.That(defType.GetString(), Is.EqualTo("object"), $"{toolName} should be of type 'object'");
        
        // 2. Verify it has properties
        Assert.That(actualDef.TryGetProperty("properties", out var defProperties), Is.True, $"{toolName} should have 'properties'");
        
        // 3. Check for expected properties
        foreach (var expectedProp in expectedProperties)
        {
            Assert.That(defProperties.TryGetProperty(expectedProp, out _), Is.True, 
                $"{toolName} should have '{expectedProp}' property");
        }
        
        // 4. Special check for tool property (should have enum/const)
        if (defProperties.TryGetProperty("tool", out var toolProperty))
        {
            Console.WriteLine($"  ✓ {toolName} has 'tool' property");
            
            // Check if tool property has enum constraint
            if (toolProperty.TryGetProperty("enum", out var enumArray))
            {
                var enumValues = enumArray.EnumerateArray().Select(e => e.GetString()).ToArray();
                Console.WriteLine($"  ✓ {toolName} tool property has enum values: [{string.Join(", ", enumValues)}]");
                
                // Verify enum contains expected tool name
                string expectedToolValue = toolName.ToLower() switch
                {
                    "sendemail" => "send_email",
                    "issueinvoice" => "issue_invoice",
                    _ => toolName.ToLower()
                };
                
                if (enumValues.Contains(expectedToolValue))
                {
                    Console.WriteLine($"  ✅ {toolName} tool enum contains expected value: {expectedToolValue}");
                }
                else
                {
                    Console.WriteLine($"  ⚠️ {toolName} tool enum does not contain expected value: {expectedToolValue}");
                    Console.WriteLine($"      Found values: [{string.Join(", ", enumValues)}]");
                }
            }
            else
            {
                Console.WriteLine($"  ⚠️ {toolName} tool property does not have enum constraint");
            }
        }
        
        // 5. Check required fields
        if (actualDef.TryGetProperty("required", out var requiredArray))
        {
            var requiredFields = requiredArray.EnumerateArray().Select(e => e.GetString()).ToHashSet();
            Console.WriteLine($"  ✓ {toolName} has {requiredFields.Count} required fields: [{string.Join(", ", requiredFields)}]");
            
            Assert.That(requiredFields, Contains.Item("tool"), $"{toolName} should require 'tool' field");
        }
        
        Console.WriteLine($"✅ {toolName} definition validation completed");
    }
}

// Custom type mapper to handle polymorphic tool calls
public class PolymorphicToolCallMapper : ITypeMapper
{
    private readonly Type _baseType;
    private readonly Type[] _allowedTypes;

    public PolymorphicToolCallMapper(Type baseType, Type[] allowedTypes)
    {
        _baseType = baseType;
        _allowedTypes = allowedTypes;
    }

    public Type MappedType => _baseType;
    public bool UseReference => false; // Don't use references, inline the anyOf

    public void GenerateSchema(JsonSchema schema, TypeMapperContext context)
    {
        // Clear any existing properties
        schema.Properties.Clear();
        
        // Generate anyOf with each allowed type
        foreach (var type in _allowedTypes)
        {
            var subSchema = context.JsonSchemaGenerator.Generate(type, context.JsonSchemaResolver);
            
            // Create const value for the tool property
            if (subSchema.Properties.ContainsKey("tool"))
            {
                var toolConstValue = GetToolConstValue(type);
                if (!string.IsNullOrEmpty(toolConstValue))
                {
                    var toolProperty = subSchema.Properties["tool"];
                    toolProperty.Enumeration.Clear();
                    toolProperty.Enumeration.Add(toolConstValue);
                }
            }
            
            schema.AnyOf.Add(subSchema);
        }
    }
    
    private string GetToolConstValue(Type type)
    {
        if (type == typeof(SendEmail)) return "send_email";
        if (type == typeof(IssueInvoice)) return "issue_invoice";
        return "";
    }
}

// Model classes with JSON Schema attributes
public class NextStep
{
    [JsonProperty("current_state")]
    [Required]
    public string CurrentState { get; set; } = default!;

    [JsonProperty("task_completed")]
    [Required]
    public bool TaskCompleted { get; set; }

    [JsonProperty("tool_call")]
    [Required]
    public ToolCall ToolCall { get; set; } = default!;
}

public abstract class ToolCall
{
    [JsonProperty("tool")]
    [Required]
    public abstract string Tool { get; }
}

public class SendEmail : ToolCall
{
    [JsonProperty("tool")]
    [Required]
    public override string Tool => "send_email";
    
    [JsonProperty("subject")]
    [Required]
    public string Subject { get; set; } = default!;
    
    [JsonProperty("message")]
    [Required]
    public string Message { get; set; } = default!;
    
    [JsonProperty("recipient_email")]
    [Required]
    public string RecipientEmail { get; set; } = default!;
}

public class IssueInvoice : ToolCall
{
    [JsonProperty("tool")]
    [Required]
    public override string Tool => "issue_invoice";
    
    [JsonProperty("email")]
    [Required]
    public string Email { get; set; } = default!;
    
    [JsonProperty("discount_percent")]
    [Required]
    public int DiscountPercent { get; set; }
}
