using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;

namespace Alkampfer.Sgr.Utils;

/// <summary>
/// **Information about a specific tool available in the schema**
/// </summary>
/// <param name="ToolName">Name of the tool (e.g., "SendEmailToolCall")</param>
/// <param name="ToolType">Type discriminator used in JSON (e.g., "send_email")</param>
/// <param name="ToolDescription">High-level description of what this tool does</param>
/// <param name="ParameterDescriptions">Markdown-formatted descriptions of all tool parameters</param>
public record ToolInformation(
    string ToolName,
    string ToolType,
    string ToolDescription,
    string ParameterDescriptions
);

/// <summary>
/// **Comprehensive result from schema generation with structured tool information**
/// </summary>
/// <param name="JsonSchema">Complete JSON schema string compatible with OpenAI structured output</param>
/// <param name="OuterObjectDescription">Description and specification of the container/outer object</param>
/// <param name="AvailableTools">Detailed information about each available inner tool</param>
public record SchemaGenerationResult(
    string JsonSchema,
    string OuterObjectDescription,
    ToolInformation[] AvailableTools
);

/// <summary>
/// Generic manager for polymorphic schema generation and deserialization.
/// Works with any container type that has a polymorphic property, allowing configuring
/// which derived types to include and providing methods for both OpenAI-compatible
/// schema generation and JSON deserialization with polymorphic support.
/// </summary>
/// <typeparam name="TContainer">The container type that holds the polymorphic property</typeparam>
/// <typeparam name="TPolymorphicBase">The base class for polymorphic types</typeparam>
public class PolymorphicSchemaManager<TContainer, TPolymorphicBase>
    where TContainer : class
    where TPolymorphicBase : class
{
    private readonly List<Type> _derivedPolymorphicTypes = new();
    private readonly Dictionary<Type, JsonSchema> _derivedSchemaCache = new();
    private readonly string _discriminatorProperty;
    private readonly PropertyInfo _polymorphicProperty;
    private readonly JsonSerializerSettings _jsonSettings;
    private readonly NewtonsoftJsonSchemaGeneratorSettings _schemaSettings;

    /// <summary>
    /// Creates a new PolymorphicSchemaManager instance
    /// </summary>
    /// <param name="discriminatorProperty">Name of the discriminator property (default: "type")</param>
    public PolymorphicSchemaManager(string discriminatorProperty = "type")
    {
        _discriminatorProperty = discriminatorProperty;
        _polymorphicProperty = FindPolymorphicProperty();

        _jsonSettings = new JsonSerializerSettings
        {
            Converters = { new GenericPolymorphicConverter<TPolymorphicBase>(_derivedPolymorphicTypes, _discriminatorProperty) },
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

    private static PropertyInfo FindPolymorphicProperty()
    {
        var containerType = typeof(TContainer);
        var polymorphicBaseType = typeof(TPolymorphicBase);

        var properties = containerType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (prop.PropertyType == polymorphicBaseType ||
                polymorphicBaseType.IsAssignableFrom(prop.PropertyType))
            {
                return prop;
            }
        }

        throw new InvalidOperationException(
            $"No property of type {polymorphicBaseType.Name} found in {containerType.Name}. " +
            $"The container type must have a property that references the polymorphic base type.");
    }

    public PolymorphicSchemaManager<TContainer, TPolymorphicBase> AddDerivedType<T>() where T : class, TPolymorphicBase
    {
        var type = typeof(T);
        if (!_derivedPolymorphicTypes.Contains(type))
        {
            _derivedPolymorphicTypes.Add(type);
            _derivedSchemaCache[type] = GenerateAndProcessDerivedSchema(type);
        }
        return this;
    }

    public PolymorphicSchemaManager<TContainer, TPolymorphicBase> AddDerivedTypes(params Type[] derivedTypes)
    {
        var polymorphicBaseType = typeof(TPolymorphicBase);

        foreach (var type in derivedTypes)
        {
            if (!polymorphicBaseType.IsAssignableFrom(type))
            {
                throw new ArgumentException($"Type {type.Name} must inherit from {polymorphicBaseType.Name}", nameof(derivedTypes));
            }

            if (!_derivedPolymorphicTypes.Contains(type))
            {
                _derivedPolymorphicTypes.Add(type);
                _derivedSchemaCache[type] = GenerateAndProcessDerivedSchema(type);
            }
        }
        return this;
    }

    public string GenerateSchema()
    {
        if (_derivedPolymorphicTypes.Count == 0)
        {
            throw new InvalidOperationException($"No derived {typeof(TPolymorphicBase).Name} types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        return GenerateSchemaInternal(_derivedPolymorphicTypes);
    }

    public string GenerateSchema(IEnumerable<Type> includedTypes)
    {
        if (_derivedPolymorphicTypes.Count == 0)
        {
            throw new InvalidOperationException($"No derived {typeof(TPolymorphicBase).Name} types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        var typesToInclude = includedTypes.ToList();

        foreach (var type in typesToInclude)
        {
            if (!_derivedPolymorphicTypes.Contains(type))
            {
                throw new ArgumentException($"Type {type.Name} is not configured in this PolymorphicSchemaManager. Add it first using AddDerivedType<T>() or AddDerivedTypes().", nameof(includedTypes));
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
        var schema = generator.Generate(typeof(TContainer));

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

        var baseTypeName = typeof(TPolymorphicBase).Name;
        schema.Definitions.Remove(baseTypeName);

        var requiredProperties = GetRequiredProperties();
        foreach (var prop in requiredProperties)
        {
            if (!schema.RequiredProperties.Contains(prop))
            {
                schema.RequiredProperties.Add(prop);
            }
        }

        foreach (var prop in requiredProperties)
        {
            if (!schema.Properties.ContainsKey(prop))
            {
                var propInfo = typeof(TContainer).GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (propInfo != null)
                {
                    var propSchema = CreatePropertySchema(propInfo);
                    schema.Properties[prop] = propSchema;
                }
            }
        }

        return schema.ToJson();
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

    private string GetPolymorphicPropertyName()
    {
        var propertyName = _polymorphicProperty.Name;
        return char.ToLower(propertyName[0]) + propertyName[1..];
    }

    private List<string> GetRequiredProperties()
    {
        var properties = typeof(TContainer).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var requiredProps = new List<string>();

        foreach (var prop in properties)
        {
            requiredProps.Add(prop.Name);
        }

        return requiredProps;
    }

    private static JsonSchemaProperty CreatePropertySchema(PropertyInfo propInfo)
    {
        var propType = propInfo.PropertyType;

        if (propType == typeof(string))
        {
            return new JsonSchemaProperty { Type = JsonObjectType.String };
        }
        else if (propType == typeof(bool))
        {
            return new JsonSchemaProperty { Type = JsonObjectType.Boolean };
        }
        else if (propType == typeof(int) || propType == typeof(int?))
        {
            return new JsonSchemaProperty { Type = JsonObjectType.Integer };
        }
        else if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var itemType = propType.GetGenericArguments()[0];
            return new JsonSchemaProperty
            {
                Type = JsonObjectType.Array,
                Item = itemType == typeof(string)
                    ? new JsonSchema { Type = JsonObjectType.String }
                    : new JsonSchema { Type = JsonObjectType.String }
            };
        }
        else
        {
            return new JsonSchemaProperty { Type = JsonObjectType.String };
        }
    }

    private JsonSchema GenerateAndProcessDerivedSchema(Type derivedType)
    {
        var generator = new JsonSchemaGenerator(_schemaSettings);
        var derivedSchema = generator.Generate(derivedType);

        var derivedName = derivedType.Name;
        var derivedDef = derivedSchema.Definitions.TryGetValue(derivedName, out var def)
            ? def
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

        if (schema.Properties.TryGetValue(discriminatorProperty, out var discriminatorProp))
        {
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

    public TContainer? DeserializeFromJson(string json)
    {
        if (_derivedPolymorphicTypes.Count == 0)
        {
            throw new InvalidOperationException($"No derived {typeof(TPolymorphicBase).Name} types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        return JsonConvert.DeserializeObject<TContainer>(json, _jsonSettings);
    }

    public static string GetDiscriminatorValue(Type type)
    {
        return type.Name;

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

    internal IReadOnlyList<Type> DerivedPolymorphicTypes => _derivedPolymorphicTypes.AsReadOnly();

    internal string DiscriminatorProperty => _discriminatorProperty;

    public PropertyInfo PolymorphicProperty => _polymorphicProperty;

    public SchemaGenerationResult GenerateSchemaWithDocumentation()
    {
        if (_derivedPolymorphicTypes.Count == 0)
        {
            throw new InvalidOperationException($"No derived {typeof(TPolymorphicBase).Name} types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        return GenerateSchemaWithDocumentationInternal(_derivedPolymorphicTypes);
    }

    public SchemaGenerationResult GenerateSchemaWithDocumentation(IEnumerable<Type> includedTypes)
    {
        if (_derivedPolymorphicTypes.Count == 0)
        {
            throw new InvalidOperationException($"No derived {typeof(TPolymorphicBase).Name} types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        var typesToInclude = includedTypes.ToList();

        foreach (var type in typesToInclude)
        {
            if (!_derivedPolymorphicTypes.Contains(type))
            {
                throw new ArgumentException($"Type {type.Name} is not configured in this PolymorphicSchemaManager. Add it first using AddDerivedType<T>() or AddDerivedTypes().", nameof(includedTypes));
            }
        }

        if (typesToInclude.Count == 0)
        {
            throw new ArgumentException("At least one type must be included in the schema.", nameof(includedTypes));
        }

        return GenerateSchemaWithDocumentationInternal(typesToInclude);
    }

    private SchemaGenerationResult GenerateSchemaWithDocumentationInternal(IEnumerable<Type> typesToInclude)
    {
        var jsonSchema = GenerateSchemaInternal(typesToInclude);
        var outerObjectDescription = GenerateOuterObjectDescription();
        var availableTools = GenerateToolInformation(typesToInclude);

        return new SchemaGenerationResult(
            JsonSchema: jsonSchema,
            OuterObjectDescription: outerObjectDescription,
            AvailableTools: availableTools
        );
    }

    private string GenerateOuterObjectDescription()
    {
        var containerType = typeof(TContainer);
        var descriptionAttribute = containerType.GetCustomAttribute<DescriptionAttribute>();
        var containerDescription = descriptionAttribute?.Description ?? $"Container object: {containerType.Name}";

        var markdown = new StringBuilder();
        markdown.AppendLine($"# {containerType.Name}");
        markdown.AppendLine();
        markdown.AppendLine(containerDescription);
        markdown.AppendLine();
        markdown.AppendLine("## Properties");
        markdown.AppendLine();

        var containerProperties = containerType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in containerProperties)
        {
            var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description provided";
            markdown.AppendLine($"- **{prop.Name}**: {description}");
        }

        return markdown.ToString();
    }

    private ToolInformation[] GenerateToolInformation(IEnumerable<Type> typesToInclude)
    {
        var tools = new List<ToolInformation>();

        foreach (var toolType in typesToInclude)
        {
            var toolDescriptionAttr = toolType.GetCustomAttribute<DescriptionAttribute>();
            var toolDescription = toolDescriptionAttr?.Description ?? $"Tool: {toolType.Name}";

            var toolTypeDiscriminator = GetDiscriminatorValue(toolType);

            var parameterDescriptions = GenerateParameterDescriptions(toolType);

            tools.Add(new ToolInformation(
                ToolName: toolType.Name,
                ToolType: toolTypeDiscriminator,
                ToolDescription: toolDescription,
                ParameterDescriptions: parameterDescriptions
            ));
        }

        return tools.ToArray();
    }

    private string GenerateParameterDescriptions(Type toolType)
    {
        var markdown = new StringBuilder();
        markdown.AppendLine($"## {toolType.Name} Parameters");
        markdown.AppendLine();

        var properties = toolType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description provided";
            markdown.AppendLine($"- **{prop.Name}**: {description}");
        }

        return markdown.ToString();
    }
}

/// <summary>
/// Generic JsonConverter for polymorphic deserialization within PolymorphicSchemaManager
/// </summary>
internal class GenericPolymorphicConverter<TPolymorphicBase> : JsonConverter<TPolymorphicBase> where TPolymorphicBase : class
{
    private readonly IReadOnlyList<Type> _derivedTypes;
    private readonly string _discriminatorProperty;

    public GenericPolymorphicConverter(IReadOnlyList<Type> derivedTypes, string discriminatorProperty)
    {
        _derivedTypes = derivedTypes;
        _discriminatorProperty = discriminatorProperty;
    }

    public override bool CanWrite => false;

    public override TPolymorphicBase ReadJson(JsonReader reader, Type objectType, TPolymorphicBase? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null!;
        }

        var jsonObject = JObject.Load(reader);

        var discriminatorToken = jsonObject[_discriminatorProperty];
        if (discriminatorToken == null)
        {
            throw new JsonSerializationException($"Missing '{_discriminatorProperty}' discriminator property for polymorphic {typeof(TPolymorphicBase).Name} deserialization");
        }

        var discriminatorValue = discriminatorToken.Value<string>()?.ToLower();

        Type? targetType = null;
        foreach (var polymorphicType in _derivedTypes)
        {
            var expectedDiscriminator = PolymorphicSchemaManager<object, TPolymorphicBase>.GetDiscriminatorValue(polymorphicType);
            if (string.Equals(discriminatorValue, expectedDiscriminator, StringComparison.OrdinalIgnoreCase))
            {
                targetType = polymorphicType;
                break;
            }
        }

        if (targetType == null)
        {
            var availableTypes = string.Join(", ", _derivedTypes.Select(t => PolymorphicSchemaManager<object, TPolymorphicBase>.GetDiscriminatorValue(t)));
            throw new JsonSerializationException($"Unknown {typeof(TPolymorphicBase).Name} type: '{discriminatorValue}'. Available types: {availableTypes}");
        }

        var target = (TPolymorphicBase)Activator.CreateInstance(targetType)!;
        serializer.Populate(jsonObject.CreateReader(), target);

        return target;
    }

    public override void WriteJson(JsonWriter writer, TPolymorphicBase? value, JsonSerializer serializer)
    {
        throw new NotImplementedException("CanWrite is false, this method should not be called");
    }
}
