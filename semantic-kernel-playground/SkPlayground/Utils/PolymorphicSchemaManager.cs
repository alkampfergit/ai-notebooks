using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;

namespace SkPlayground.Utils;

/// <summary>
/// **Result object containing schema and documentation information**
///
/// This record provides a comprehensive result from schema generation including:
/// - **JsonSchema**: The complete JSON schema string for OpenAI compatibility
/// - **PropertyDescriptions**: Markdown-formatted documentation of all properties
/// - **ToolDescription**: High-level description of the tool/container class
/// </summary>
/// <param name="JsonSchema">Complete JSON schema string compatible with OpenAI structured output</param>
/// <param name="PropertyDescriptions">Markdown-formatted documentation of all properties and their descriptions</param>
/// <param name="ToolDescription">High-level description of the tool/container class extracted from Description attribute</param>
public record SchemaGenerationResult(
    string JsonSchema,
    string PropertyDescriptions,
    string ToolDescription
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
    /// <exception cref="InvalidOperationException">Thrown when no polymorphic property is found</exception>
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
    /// Uses reflection to find the property in TContainer that has type TPolymorphicBase
    /// </summary>
    /// <returns>PropertyInfo for the polymorphic property</returns>
    /// <exception cref="InvalidOperationException">Thrown when no matching property is found</exception>
    private static PropertyInfo FindPolymorphicProperty()
    {
        var containerType = typeof(TContainer);
        var polymorphicBaseType = typeof(TPolymorphicBase);

        // Find property that is exactly TPolymorphicBase or is assignable to TPolymorphicBase
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

    /// <summary>
    /// Adds a derived polymorphic type to be included in schema generation and deserialization
    /// </summary>
    /// <typeparam name="T">The derived type to add (must inherit from TPolymorphicBase)</typeparam>
    /// <returns>This instance for method chaining</returns>
    public PolymorphicSchemaManager<TContainer, TPolymorphicBase> AddDerivedType<T>() where T : class, TPolymorphicBase
    {
        var type = typeof(T);
        if (!_derivedPolymorphicTypes.Contains(type))
        {
            _derivedPolymorphicTypes.Add(type);
            // Pre-generate and cache the schema for this type
            _derivedSchemaCache[type] = GenerateAndProcessDerivedSchema(type);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple derived polymorphic types to be included in schema generation and deserialization
    /// </summary>
    /// <param name="derivedTypes">Array of derived types to add</param>
    /// <returns>This instance for method chaining</returns>
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
                // Pre-generate and cache the schema for this type
                _derivedSchemaCache[type] = GenerateAndProcessDerivedSchema(type);
            }
        }
        return this;
    }

    /// <summary>
    /// Generates an OpenAI-compatible JSON schema for TContainer with the configured derived polymorphic types
    /// </summary>
    /// <returns>JSON schema string compatible with OpenAI structured output</returns>
    public string GenerateSchema()
    {
        if (_derivedPolymorphicTypes.Count == 0)
        {
            throw new InvalidOperationException($"No derived {typeof(TPolymorphicBase).Name} types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        return GenerateSchemaInternal(_derivedPolymorphicTypes);
    }

    /// <summary>
    /// Generates an OpenAI-compatible JSON schema for TContainer with only the specified derived types
    /// </summary>
    /// <param name="includedTypes">The specific derived types to include in the schema</param>
    /// <returns>JSON schema string compatible with OpenAI structured output</returns>
    public string GenerateSchema(IEnumerable<Type> includedTypes)
    {
        if (_derivedPolymorphicTypes.Count == 0)
        {
            throw new InvalidOperationException($"No derived {typeof(TPolymorphicBase).Name} types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        var typesToInclude = includedTypes.ToList();
        
        // Validate that all included types are configured in this manager
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

    /// <summary>
    /// Internal method to generate schema using cached derived schemas
    /// </summary>
    private string GenerateSchemaInternal(IEnumerable<Type> typesToInclude)
    {
        var generator = new JsonSchemaGenerator(_schemaSettings);
        var schema = generator.Generate(typeof(TContainer));

        // Ensure root schema is an object and has all required properties
        schema.Type = JsonObjectType.Object;
        schema.AllowAdditionalProperties = false;

        // Find the polymorphic property (exact same logic as NextStepManager)
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

        // Remove the abstract base class definition as it's not needed and causes OpenAI rejection
        var baseTypeName = typeof(TPolymorphicBase).Name;
        schema.Definitions.Remove(baseTypeName);

        // Get required properties from the container type using reflection
        var requiredProperties = GetRequiredProperties();
        foreach (var prop in requiredProperties)
        {
            if (!schema.RequiredProperties.Contains(prop))
            {
                schema.RequiredProperties.Add(prop);
            }
        }

        // Ensure all required properties exist in the schema
        foreach (var prop in requiredProperties)
        {
            if (!schema.Properties.ContainsKey(prop))
            {
                // Try to infer property type from reflection
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

    /// <summary>
    /// Finds the polymorphic property by looking for properties that reference abstract types
    /// (copied from NextStepManager)
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
    /// Gets the name of the polymorphic property using camelCase naming convention
    /// </summary>
    private string GetPolymorphicPropertyName()
    {
        var propertyName = _polymorphicProperty.Name;
        // Convert to camelCase (e.g., "ToolCall" -> "toolCall")
        return char.ToLower(propertyName[0]) + propertyName[1..];
    }

    /// <summary>
    /// Gets required properties from the container type using reflection
    /// </summary>
    private List<string> GetRequiredProperties()
    {
        var properties = typeof(TContainer).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var requiredProps = new List<string>();

        foreach (var prop in properties)
        {
            // Use PascalCase to match NJsonSchema default naming convention
            requiredProps.Add(prop.Name);
        }

        return requiredProps;
    }

    /// <summary>
    /// Creates a JsonSchemaProperty based on a PropertyInfo
    /// </summary>
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
                    : new JsonSchema { Type = JsonObjectType.String } // Default to string for unknown types
            };
        }
        else
        {
            return new JsonSchemaProperty { Type = JsonObjectType.String }; // Default fallback
        }
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
        var derivedDef = derivedSchema.Definitions.TryGetValue(derivedName, out var def) 
            ? def 
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
        if (schema.Properties.TryGetValue(discriminatorProperty, out var discriminatorProp))
        {
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
    /// Deserializes JSON into a TContainer object with polymorphic support
    /// </summary>
    /// <param name="json">JSON string to deserialize</param>
    /// <returns>Deserialized TContainer with correctly typed polymorphic property</returns>
    public TContainer? DeserializeFromJson(string json)
    {
        if (_derivedPolymorphicTypes.Count == 0)
        {
            throw new InvalidOperationException($"No derived {typeof(TPolymorphicBase).Name} types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        return JsonConvert.DeserializeObject<TContainer>(json, _jsonSettings);
    }

    /// <summary>
    /// Gets the discriminator value for a given type (converts PascalCase to snake_case)
    /// </summary>
    /// <param name="type">The polymorphic type</param>
    /// <returns>Discriminator value string</returns>
    public static string GetDiscriminatorValue(Type type)
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
    /// Gets all configured derived polymorphic types
    /// </summary>
    internal IReadOnlyList<Type> DerivedPolymorphicTypes => _derivedPolymorphicTypes.AsReadOnly();

    /// <summary>
    /// Gets the configured discriminator property name
    /// </summary>
    internal string DiscriminatorProperty => _discriminatorProperty;

    /// <summary>
    /// Gets the polymorphic property information
    /// </summary>
    public PropertyInfo PolymorphicProperty => _polymorphicProperty;

    /// <summary>
    /// **Generates comprehensive schema result with documentation**
    ///
    /// Creates a complete schema generation result including the JSON schema,
    /// property descriptions in markdown format, and tool description.
    /// </summary>
    /// <returns>SchemaGenerationResult containing schema, property docs, and tool description</returns>
    public SchemaGenerationResult GenerateSchemaWithDocumentation()
    {
        if (_derivedPolymorphicTypes.Count == 0)
        {
            throw new InvalidOperationException($"No derived {typeof(TPolymorphicBase).Name} types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        return GenerateSchemaWithDocumentationInternal(_derivedPolymorphicTypes);
    }

    /// <summary>
    /// **Generates comprehensive schema result with documentation for specific types**
    ///
    /// Creates a complete schema generation result including the JSON schema,
    /// property descriptions in markdown format, and tool description for only
    /// the specified derived types.
    /// </summary>
    /// <param name="includedTypes">The specific derived types to include in the schema</param>
    /// <returns>SchemaGenerationResult containing schema, property docs, and tool description</returns>
    public SchemaGenerationResult GenerateSchemaWithDocumentation(IEnumerable<Type> includedTypes)
    {
        if (_derivedPolymorphicTypes.Count == 0)
        {
            throw new InvalidOperationException($"No derived {typeof(TPolymorphicBase).Name} types have been added. Use AddDerivedType<T>() or AddDerivedTypes() first.");
        }

        var typesToInclude = includedTypes.ToList();

        // Validate that all included types are configured in this manager
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

    /// <summary>
    /// **Internal method to generate comprehensive schema result**
    ///
    /// Generates the JSON schema and extracts documentation from Description attributes
    /// to create property descriptions and tool description.
    /// </summary>
    /// <param name="typesToInclude">Types to include in the schema</param>
    /// <returns>Complete schema generation result</returns>
    private SchemaGenerationResult GenerateSchemaWithDocumentationInternal(IEnumerable<Type> typesToInclude)
    {
        // Generate the JSON schema using existing logic
        var jsonSchema = GenerateSchemaInternal(typesToInclude);

        // Extract tool description from container class
        var toolDescription = ExtractToolDescription();

        // Generate property descriptions in markdown format
        var propertyDescriptions = GeneratePropertyDescriptions(typesToInclude);

        return new SchemaGenerationResult(
            JsonSchema: jsonSchema,
            PropertyDescriptions: propertyDescriptions,
            ToolDescription: toolDescription
        );
    }

    /// <summary>
    /// **Extracts tool description from container class Description attribute**
    ///
    /// Looks for Description attribute on the container class (TContainer)
    /// and returns its value, or a default message if not found.
    /// </summary>
    /// <returns>Tool description string</returns>
    private string ExtractToolDescription()
    {
        var containerType = typeof(TContainer);
        var descriptionAttribute = containerType.GetCustomAttribute<DescriptionAttribute>();

        return descriptionAttribute?.Description ?? $"Tool: {containerType.Name}";
    }

    /// <summary>
    /// **Generates markdown-formatted property descriptions**
    ///
    /// Creates comprehensive documentation for all properties in the container
    /// and polymorphic types, including their descriptions from Description attributes.
    /// </summary>
    /// <param name="typesToInclude">Polymorphic types to include in documentation</param>
    /// <returns>Markdown-formatted property documentation</returns>
    private string GeneratePropertyDescriptions(IEnumerable<Type> typesToInclude)
    {
        var markdown = new StringBuilder();
        markdown.AppendLine("# Property Descriptions");
        markdown.AppendLine();

        // Document container properties
        var containerType = typeof(TContainer);
        markdown.AppendLine($"## {containerType.Name} Properties");
        markdown.AppendLine();

        var containerProperties = containerType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in containerProperties)
        {
            var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description provided";
            markdown.AppendLine($"- **{prop.Name}**: {description}");
        }

        markdown.AppendLine();

        // Document polymorphic type properties
        foreach (var type in typesToInclude)
        {
            markdown.AppendLine($"## {type.Name} Properties");
            markdown.AppendLine();

            var typeProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in typeProperties)
            {
                var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description provided";
                markdown.AppendLine($"- **{prop.Name}**: {description}");
            }

            markdown.AppendLine();
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

    public override bool CanWrite => false; // Only handle reading/deserialization
    
    public override TPolymorphicBase ReadJson(JsonReader reader, Type objectType, TPolymorphicBase? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null!;
        }

        // Load the JSON object
        var jsonObject = JObject.Load(reader);
        
        // Get the discriminator value
        var discriminatorToken = jsonObject[_discriminatorProperty];
        if (discriminatorToken == null)
        {
            throw new JsonSerializationException($"Missing '{_discriminatorProperty}' discriminator property for polymorphic {typeof(TPolymorphicBase).Name} deserialization");
        }
        
        var discriminatorValue = discriminatorToken.Value<string>()?.ToLower();
        
        // Find the matching polymorphic type based on discriminator
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

        // Create and populate the target object
        var target = (TPolymorphicBase)Activator.CreateInstance(targetType)!;
        serializer.Populate(jsonObject.CreateReader(), target);
        
        return target;
    }
    
    public override void WriteJson(JsonWriter writer, TPolymorphicBase? value, JsonSerializer serializer)
    {
        throw new NotImplementedException("CanWrite is false, this method should not be called");
    }
}