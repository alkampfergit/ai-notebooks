using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using SkPlayground.Models;
using SkPlayground.BusinessFunctions;

namespace SkPlayground.Utils;

/// <summary>
/// Unified manager for NextStep schema generation and polymorphic deserialization.
/// Allows configuring which derived ToolCall types to include and provides methods for both
/// OpenAI-compatible schema generation and JSON deserialization with polymorphic support.
/// </summary>
public class NextStepManager
{
    private readonly List<Type> _derivedToolCallTypes = new();
    private readonly Dictionary<Type, JsonSchema> _derivedSchemaCache = new();
    private readonly string _discriminatorProperty;
    private readonly JsonSerializerSettings _jsonSettings;
    private readonly NewtonsoftJsonSchemaGeneratorSettings _schemaSettings;

    /// <summary>
    /// Creates a new NextStepManager instance
    /// </summary>
    /// <param name="discriminatorProperty">Name of the discriminator property (default: "type")</param>
    public NextStepManager(string discriminatorProperty = "type")
    {
        _discriminatorProperty = discriminatorProperty;
        _jsonSettings = new JsonSerializerSettings
        {
            Converters = { new NextStepPolymorphicConverter(this) },
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        // Initialize schema generator settings - consistent settings for all schema generation
        _schemaSettings = new NewtonsoftJsonSchemaGeneratorSettings
        {
            SchemaType = SchemaType.JsonSchema,
            DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull,
            FlattenInheritanceHierarchy = false,
            GenerateAbstractProperties = false,
            AlwaysAllowAdditionalObjectProperties = false
        };
    }

    /// <summary>
    /// Adds a derived ToolCall type to be included in schema generation and deserialization
    /// </summary>
    /// <typeparam name="T">The derived ToolCall type to add (must inherit from ToolCall)</typeparam>
    /// <returns>This instance for method chaining</returns>
    public NextStepManager AddDerivedType<T>() where T : ToolCall
    {
        var type = typeof(T);
        if (!_derivedToolCallTypes.Contains(type))
        {
            _derivedToolCallTypes.Add(type);
            // Pre-generate and cache the schema for this type
            _derivedSchemaCache[type] = GenerateAndProcessDerivedSchema(type);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple derived ToolCall types to be included in schema generation and deserialization
    /// </summary>
    /// <param name="derivedTypes">Array of derived ToolCall types to add</param>
    /// <returns>This instance for method chaining</returns>
    public NextStepManager AddDerivedTypes(params Type[] derivedTypes)
    {
        foreach (var type in derivedTypes)
        {
            if (!typeof(ToolCall).IsAssignableFrom(type))
            {
                throw new ArgumentException($"Type {type.Name} must inherit from ToolCall", nameof(derivedTypes));
            }
            
            if (!_derivedToolCallTypes.Contains(type))
            {
                _derivedToolCallTypes.Add(type);
                // Pre-generate and cache the schema for this type
                _derivedSchemaCache[type] = GenerateAndProcessDerivedSchema(type);
            }
        }
        return this;
    }

    /// <summary>
    /// Generates an OpenAI-compatible JSON schema for NextStep with the configured derived ToolCall types
    /// </summary>
    /// <returns>JSON schema string compatible with OpenAI structured output</returns>
    public string GenerateSchema()
    {
        if (_derivedToolCallTypes.Count == 0)
        {
            throw new InvalidOperationException("No derived ToolCall types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        return GenerateSchemaInternal(_derivedToolCallTypes);
    }

    /// <summary>
    /// Generates an OpenAI-compatible JSON schema for NextStep with only the specified derived ToolCall types
    /// </summary>
    /// <param name="includedTypes">The specific derived ToolCall types to include in the schema</param>
    /// <returns>JSON schema string compatible with OpenAI structured output</returns>
    public string GenerateSchema(IEnumerable<Type> includedTypes)
    {
        if (_derivedToolCallTypes.Count == 0)
        {
            throw new InvalidOperationException("No derived ToolCall types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        var typesToInclude = includedTypes.ToList();
        
        // Validate that all included types are configured in this manager
        foreach (var type in typesToInclude)
        {
            if (!_derivedToolCallTypes.Contains(type))
            {
                throw new ArgumentException($"Type {type.Name} is not configured in this NextStepManager. Add it first using AddDerivedType<T>() or AddDerivedTypes().", nameof(includedTypes));
            }
        }

        if (typesToInclude.Count == 0)
        {
            throw new ArgumentException("At least one type must be included in the schema.", nameof(includedTypes));
        }

        return GenerateSchemaInternal(typesToInclude);
    }

    /// <summary>
    /// Internal method to generate schema using cached derived schemas
    /// </summary>
    private string GenerateSchemaInternal(IEnumerable<Type> typesToInclude)
    {
        var generator = new JsonSchemaGenerator(_schemaSettings);
        var schema = generator.Generate(typeof(NextStep));

        // Ensure root schema is an object and has all required properties
        schema.Type = JsonObjectType.Object;
        schema.AllowAdditionalProperties = false;

        // Find the polymorphic property (ToolCall property)
        var targetProperty = FindPolymorphicProperty(schema);
        
        if (!string.IsNullOrEmpty(targetProperty) && schema.Properties.ContainsKey(targetProperty))
        {
            var polymorphicProp = schema.Properties[targetProperty];
            
            // Use cached schemas for the specified types
            var includedTypesList = typesToInclude.ToList();
            
            // Add cached schemas to the main schema definitions
            foreach (var type in includedTypesList)
            {
                if (_derivedSchemaCache.TryGetValue(type, out var cachedSchema))
                {
                    schema.Definitions[type.Name] = cachedSchema;
                }
            }
            
            // Replace polymorphic property with anyOf constraint (OpenAI pattern)
            polymorphicProp.Reference = null;
            polymorphicProp.AnyOf.Clear();
            polymorphicProp.OneOf.Clear(); // Also clear any oneOf references to abstract classes
            
            foreach (var type in includedTypesList)
            {
                polymorphicProp.AnyOf.Add(new JsonSchema
                {
                    Reference = schema.Definitions[type.Name]
                });
            }
        }

        // Remove the abstract ToolCall definition as it's not needed and causes OpenAI rejection
        if (schema.Definitions.ContainsKey("ToolCall"))
        {
            schema.Definitions.Remove("ToolCall");
        }

        // Ensure required properties are present
        var required = new[] { "CurrentState", "PlanRemainingStepsBrief", "TaskCompleted", "NextStepToolToCall" };
        foreach (var prop in required)
        {
            if (!schema.RequiredProperties.Contains(prop))
            {
                schema.RequiredProperties.Add(prop);
            }
        }

        // Ensure all required properties exist in the schema
        foreach (var prop in required)
        {
            if (!schema.Properties.ContainsKey(prop))
            {
                // Set appropriate types for NextStep properties
                var propSchema = prop switch
                {
                    "CurrentState" => new JsonSchemaProperty { Type = JsonObjectType.String },
                    "PlanRemainingStepsBrief" => new JsonSchemaProperty
                    {
                        Type = JsonObjectType.Array,
                        Item = new JsonSchema { Type = JsonObjectType.String }
                    },
                    "TaskCompleted" => new JsonSchemaProperty { Type = JsonObjectType.Boolean },
                    _ => new JsonSchemaProperty { Type = JsonObjectType.String }
                };
                schema.Properties[prop] = propSchema;
            }
        }

        return schema.ToJson();
    }

    /// <summary>
    /// Generates and processes a schema for a specific derived type
    /// </summary>
    private JsonSchema GenerateAndProcessDerivedSchema(Type derivedType)
    {
        var generator = new JsonSchemaGenerator(_schemaSettings);
        var derivedSchema = generator.Generate(derivedType);
        
        // Extract and flatten the derived schema
        var derivedName = derivedType.Name;
        var derivedDef = derivedSchema.Definitions.ContainsKey(derivedName) 
            ? derivedSchema.Definitions[derivedName] 
            : derivedSchema;
        
        var flattened = FlattenInheritanceSchema(derivedDef);
        
        // Add const/enum discriminators
        AddDiscriminatorToSchema(flattened, _discriminatorProperty, GetDiscriminatorValue(derivedType));
        
        return flattened;
    }

    /// <summary>
    /// Flattens inheritance schema by removing allOf patterns and merging properties
    /// </summary>
    private static JsonSchema FlattenInheritanceSchema(JsonSchema schema)
    {
        var flattened = new JsonSchema
        {
            Type = JsonObjectType.Object,
            AllowAdditionalProperties = false
        };

        // Add properties from allOf sections if they exist
        if (schema.AllOf.Count > 0)
        {
            foreach (var allOfItem in schema.AllOf)
            {
                foreach (var prop in allOfItem.Properties)
                {
                    flattened.Properties[prop.Key] = prop.Value;
                }
                foreach (var req in allOfItem.RequiredProperties)
                {
                    flattened.RequiredProperties.Add(req);
                }
            }
        }
        else
        {
            // If no allOf, copy properties directly
            foreach (var prop in schema.Properties)
            {
                flattened.Properties[prop.Key] = prop.Value;
            }
            foreach (var req in schema.RequiredProperties)
            {
                flattened.RequiredProperties.Add(req);
            }
        }

        // For OpenAI compatibility with additionalProperties: false,
        // ALL properties must be in the required array, not just those with [Required] attributes
        foreach (var property in flattened.Properties)
        {
            if (!flattened.RequiredProperties.Contains(property.Key))
            {
                flattened.RequiredProperties.Add(property.Key);
            }
        }

        return flattened;
    }

    /// <summary>
    /// Adds const/enum discriminator to the specified property in the schema
    /// </summary>
    private static void AddDiscriminatorToSchema(JsonSchema schema, string discriminatorProperty, string discriminatorValue)
    {
        // Ensure discriminator property is required
        if (!schema.RequiredProperties.Contains(discriminatorProperty))
        {
            schema.RequiredProperties.Add(discriminatorProperty);
        }

        // Add const/enum discriminators to the discriminator property (OpenAI pattern)
        if (schema.Properties.ContainsKey(discriminatorProperty))
        {
            var discriminatorProp = schema.Properties[discriminatorProperty];
            discriminatorProp.Enumeration.Clear();
            discriminatorProp.Enumeration.Add(discriminatorValue);
            discriminatorProp.Title = char.ToUpper(discriminatorProperty[0]) + discriminatorProperty[1..];
            
            // Set const value using ExtensionData
            discriminatorProp.ExtensionData ??= new Dictionary<string, object?>();
            discriminatorProp.ExtensionData["const"] = discriminatorValue;
        }
        else
        {
            // If discriminator property doesn't exist, create it
            schema.Properties[discriminatorProperty] = new JsonSchemaProperty
            {
                Type = JsonObjectType.String,
                Title = char.ToUpper(discriminatorProperty[0]) + discriminatorProperty[1..],
                Enumeration = { discriminatorValue },
                ExtensionData = new Dictionary<string, object?> { ["const"] = discriminatorValue }
            };
        }
    }

    /// <summary>
    /// Finds the polymorphic property by looking for properties that reference abstract types
    /// </summary>
    private static string FindPolymorphicProperty(JsonSchema schema)
    {
        foreach (var property in schema.Properties)
        {
            if (property.Value.Reference != null || property.Value.AnyOf.Count > 0 || property.Value.OneOf.Count > 0)
            {
                return property.Key;
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Deserializes JSON into a NextStep object with polymorphic ToolCall support
    /// </summary>
    /// <param name="json">JSON string to deserialize</param>
    /// <returns>Deserialized NextStep with correctly typed ToolCall property</returns>
    public NextStep? DeserializeFromJson(string json)
    {
        if (_derivedToolCallTypes.Count == 0)
        {
            throw new InvalidOperationException("No derived ToolCall types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        return JsonConvert.DeserializeObject<NextStep>(json, _jsonSettings);
    }

    /// <summary>
    /// Gets the discriminator value for a given ToolCall type (converts PascalCase to snake_case)
    /// </summary>
    /// <param name="type">The ToolCall type</param>
    /// <returns>Discriminator value string</returns>
    public string GetDiscriminatorValue(Type type)
    {
        // Convert PascalCase to snake_case: "SendEmailToolCall" -> "send_email_tool_call"
        var name = type.Name;
        var result = string.Empty;
        
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                result += "_";
            }
            result += char.ToLower(name[i]);
        }
        
        return result;
    }

    /// <summary>
    /// Gets all configured derived ToolCall types
    /// </summary>
    internal IReadOnlyList<Type> DerivedToolCallTypes => _derivedToolCallTypes.AsReadOnly();

    /// <summary>
    /// Gets the configured discriminator property name
    /// </summary>
    internal string DiscriminatorProperty => _discriminatorProperty;
}

/// <summary>
/// Custom JsonConverter for polymorphic ToolCall deserialization within NextStepManager
/// </summary>
internal class NextStepPolymorphicConverter : JsonConverter<ToolCall>
{
    private readonly NextStepManager _manager;

    public NextStepPolymorphicConverter(NextStepManager manager)
    {
        _manager = manager;
    }

    public override bool CanWrite => false; // Only handle reading/deserialization
    
    public override ToolCall ReadJson(JsonReader reader, Type objectType, ToolCall? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null!;
        }

        // Load the JSON object
        var jsonObject = JObject.Load(reader);
        
        // Get the discriminator value
        var discriminatorToken = jsonObject[_manager.DiscriminatorProperty];
        if (discriminatorToken == null)
        {
            throw new JsonSerializationException($"Missing '{_manager.DiscriminatorProperty}' discriminator property for polymorphic ToolCall deserialization");
        }
        
        var discriminatorValue = discriminatorToken.Value<string>()?.ToLower();
        
        // Find the matching ToolCall type based on discriminator
        Type? targetType = null;
        foreach (var toolCallType in _manager.DerivedToolCallTypes)
        {
            var expectedDiscriminator = _manager.GetDiscriminatorValue(toolCallType);
            if (string.Equals(discriminatorValue, expectedDiscriminator, StringComparison.OrdinalIgnoreCase))
            {
                targetType = toolCallType;
                break;
            }
        }

        if (targetType == null)
        {
            var availableTypes = string.Join(", ", _manager.DerivedToolCallTypes.Select(t => _manager.GetDiscriminatorValue(t)));
            throw new JsonSerializationException($"Unknown tool call type: '{discriminatorValue}'. Available types: {availableTypes}");
        }

        // Create and populate the target object
        var target = (ToolCall)Activator.CreateInstance(targetType)!;
        serializer.Populate(jsonObject.CreateReader(), target);
        
        return target;
    }
    
    public override void WriteJson(JsonWriter writer, ToolCall? value, JsonSerializer serializer)
    {
        throw new NotImplementedException("CanWrite is false, this method should not be called");
    }
}