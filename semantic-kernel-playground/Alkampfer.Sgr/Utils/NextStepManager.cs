using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using Alkampfer.Sgr.Models;
using Alkampfer.Sgr.BusinessFunctions;

namespace Alkampfer.Sgr.Utils;

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

    public NextStepManager(string discriminatorProperty = "type")
    {
        _discriminatorProperty = discriminatorProperty;
        _jsonSettings = new JsonSerializerSettings
        {
            Converters = { new NextStepPolymorphicConverter(this) },
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        _schemaSettings = new NewtonsoftJsonSchemaGeneratorSettings
        {
            SchemaType = SchemaType.JsonSchema,
            DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull,
            FlattenInheritanceHierarchy = false,
            GenerateAbstractProperties = false,
            AlwaysAllowAdditionalObjectProperties = false
        };
    }

    public NextStepManager AddDerivedType<T>() where T : ToolCall
    {
        var type = typeof(T);
        if (!_derivedToolCallTypes.Contains(type))
        {
            _derivedToolCallTypes.Add(type);
            _derivedSchemaCache[type] = GenerateAndProcessDerivedSchema(type);
        }
        return this;
    }

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
                _derivedSchemaCache[type] = GenerateAndProcessDerivedSchema(type);
            }
        }
        return this;
    }

    public string GenerateSchema()
    {
        if (_derivedToolCallTypes.Count == 0)
        {
            throw new InvalidOperationException("No derived ToolCall types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        return GenerateSchemaInternal(_derivedToolCallTypes);
    }

    public string GenerateSchema(IEnumerable<Type> includedTypes)
    {
        if (_derivedToolCallTypes.Count == 0)
        {
            throw new InvalidOperationException("No derived ToolCall types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        var typesToInclude = includedTypes.ToList();

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

    private string GenerateSchemaInternal(IEnumerable<Type> typesToInclude)
    {
        var generator = new JsonSchemaGenerator(_schemaSettings);
        var schema = generator.Generate(typeof(NextStep));

        schema.Type = JsonObjectType.Object;
        schema.AllowAdditionalProperties = false;

        var targetProperty = FindPolymorphicProperty(schema);

        if (!string.IsNullOrEmpty(targetProperty) && schema.Properties.ContainsKey(targetProperty))
        {
            var polymorphicProp = schema.Properties[targetProperty];

            var includedTypesList = typesToInclude.ToList();

            foreach (var type in includedTypesList)
            {
                if (_derivedSchemaCache.TryGetValue(type, out var cachedSchema))
                {
                    schema.Definitions[type.Name] = cachedSchema;
                }
            }

            polymorphicProp.Reference = null;
            polymorphicProp.AnyOf.Clear();
            polymorphicProp.OneOf.Clear();

            foreach (var type in includedTypesList)
            {
                polymorphicProp.AnyOf.Add(new JsonSchema
                {
                    Reference = schema.Definitions[type.Name]
                });
            }
        }

        if (schema.Definitions.ContainsKey("ToolCall"))
        {
            schema.Definitions.Remove("ToolCall");
        }

        var required = new[] { "CurrentState", "PlanRemainingStepsBrief", "TaskCompleted", "Function" };
        foreach (var prop in required)
        {
            if (!schema.RequiredProperties.Contains(prop))
            {
                schema.RequiredProperties.Add(prop);
            }
        }

        foreach (var prop in required)
        {
            if (!schema.Properties.ContainsKey(prop))
            {
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

    private JsonSchema GenerateAndProcessDerivedSchema(Type derivedType)
    {
        var generator = new JsonSchemaGenerator(_schemaSettings);
        var derivedSchema = generator.Generate(derivedType);

        var derivedName = derivedType.Name;
        var derivedDef = derivedSchema.Definitions.ContainsKey(derivedName)
            ? derivedSchema.Definitions[derivedName]
            : derivedSchema;

        var flattened = FlattenInheritanceSchema(derivedDef);

        AddDiscriminatorToSchema(flattened, _discriminatorProperty, GetDiscriminatorValue(derivedType));

        return flattened;
    }

    private static JsonSchema FlattenInheritanceSchema(JsonSchema schema)
    {
        var flattened = new JsonSchema
        {
            Type = JsonObjectType.Object,
            AllowAdditionalProperties = false
        };

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
            foreach (var prop in schema.Properties)
            {
                flattened.Properties[prop.Key] = prop.Value;
            }
            foreach (var req in schema.RequiredProperties)
            {
                flattened.RequiredProperties.Add(req);
            }
        }

        foreach (var property in flattened.Properties)
        {
            if (!flattened.RequiredProperties.Contains(property.Key))
            {
                flattened.RequiredProperties.Add(property.Key);
            }
        }

        return flattened;
    }

    private static void AddDiscriminatorToSchema(JsonSchema schema, string discriminatorProperty, string discriminatorValue)
    {
        if (!schema.RequiredProperties.Contains(discriminatorProperty))
        {
            schema.RequiredProperties.Add(discriminatorProperty);
        }

        if (schema.Properties.ContainsKey(discriminatorProperty))
        {
            var discriminatorProp = schema.Properties[discriminatorProperty];
            discriminatorProp.Enumeration.Clear();
            discriminatorProp.Enumeration.Add(discriminatorValue);
            discriminatorProp.Title = char.ToUpper(discriminatorProperty[0]) + discriminatorProperty[1..];

            discriminatorProp.ExtensionData ??= new Dictionary<string, object?>();
            discriminatorProp.ExtensionData["const"] = discriminatorValue;
        }
        else
        {
            schema.Properties[discriminatorProperty] = new JsonSchemaProperty
            {
                Type = JsonObjectType.String,
                Title = char.ToUpper(discriminatorProperty[0]) + discriminatorProperty[1..],
                Enumeration = { discriminatorValue },
                ExtensionData = new Dictionary<string, object?> { ["const"] = discriminatorValue }
            };
        }
    }

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

    public NextStep? DeserializeFromJson(string json)
    {
        if (_derivedToolCallTypes.Count == 0)
        {
            throw new InvalidOperationException("No derived ToolCall types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        return JsonConvert.DeserializeObject<NextStep>(json, _jsonSettings);
    }

    public string GetDiscriminatorValue(Type type)
    {
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

    internal IReadOnlyList<Type> DerivedToolCallTypes => _derivedToolCallTypes.AsReadOnly();

    internal string DiscriminatorProperty => _discriminatorProperty;
}

internal class NextStepPolymorphicConverter : JsonConverter<ToolCall>
{
    private readonly NextStepManager _manager;

    public NextStepPolymorphicConverter(NextStepManager manager)
    {
        _manager = manager;
    }

    public override bool CanWrite => false;

    public override ToolCall ReadJson(JsonReader reader, Type objectType, ToolCall? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null!;
        }

        var jsonObject = JObject.Load(reader);

        var discriminatorToken = jsonObject[_manager.DiscriminatorProperty];
        if (discriminatorToken == null)
        {
            throw new JsonSerializationException($"Missing '{_manager.DiscriminatorProperty}' discriminator property for polymorphic ToolCall deserialization");
        }

        var discriminatorValue = discriminatorToken.Value<string>()?.ToLower();

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

        var target = (ToolCall)Activator.CreateInstance(targetType)!;
        serializer.Populate(jsonObject.CreateReader(), target);

        return target;
    }

    public override void WriteJson(JsonWriter writer, ToolCall? value, JsonSerializer serializer)
    {
        throw new NotImplementedException("CanWrite is false, this method should not be called");
    }
}
