using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using System.ComponentModel.DataAnnotations;

namespace SkPlaygroundTests;

/// <summary>
/// Utility class for generating OpenAI-compatible polymorphic JSON schemas
/// that handle inheritance with anyOf patterns and const discriminators
/// </summary>
public static class PolymorphicSchemaGenerator
{
    /// <summary>
    /// Generates a polymorphic JSON schema for the specified base type with derived types.
    /// The schema is optimized for OpenAI structured output compatibility.
    /// </summary>
    /// <typeparam name="TBase">The base/root type to generate schema for</typeparam>
    /// <param name="derivedTypes">Array of derived types to include in the polymorphic schema</param>
    /// <param name="discriminatorProperty">Name of the discriminator property (default: "type")</param>
    /// <param name="polymorphicProperty">Name of the property that should use polymorphic schema (default: auto-detect)</param>
    /// <returns>JSON schema string compatible with OpenAI structured output</returns>
    public static string GeneratePolymorphicJsonSchema<TBase>(
        Type[] derivedTypes,
        string discriminatorProperty = "type",
        string? polymorphicProperty = null)
    {
        var settings = new NewtonsoftJsonSchemaGeneratorSettings
        {
            SchemaType = SchemaType.JsonSchema,
            DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull,
            FlattenInheritanceHierarchy = false,
            GenerateAbstractProperties = false,
            AlwaysAllowAdditionalObjectProperties = false
        };

        var generator = new JsonSchemaGenerator(settings);
        var schema = generator.Generate(typeof(TBase));

        // Find the polymorphic property - either specified or auto-detect the first abstract/interface property
        string targetProperty = polymorphicProperty ?? FindPolymorphicProperty(schema);
        
        if (!string.IsNullOrEmpty(targetProperty) && schema.Properties.ContainsKey(targetProperty))
        {
            var polymorphicProp = schema.Properties[targetProperty];
            
            // Generate and flatten derived schemas
            var derivedSchemas = new Dictionary<string, JsonSchema>();
            
            foreach (var derivedType in derivedTypes)
            {
                var derivedSchema = generator.Generate(derivedType);
                var derivedName = derivedType.Name;
                
                // Extract and flatten the derived schema
                var derivedDef = derivedSchema.Definitions.ContainsKey(derivedName) 
                    ? derivedSchema.Definitions[derivedName] 
                    : derivedSchema;
                
                var flattened = FlattenInheritanceSchema(derivedDef);
                
                // Add const/enum discriminators
                AddDiscriminatorToSchema(flattened, discriminatorProperty, GetDiscriminatorValue(derivedType));
                
                derivedSchemas[derivedName] = flattened;
                schema.Definitions[derivedName] = flattened;
            }
            
            // Replace polymorphic property with anyOf constraint (OpenAI pattern)
            polymorphicProp.Reference = null;
            polymorphicProp.AnyOf.Clear();
            
            foreach (var derivedName in derivedSchemas.Keys)
            {
                polymorphicProp.AnyOf.Add(new JsonSchema
                {
                    Reference = schema.Definitions[derivedName]
                });
            }
        }

        return schema.ToJson();
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
    }

    /// <summary>
    /// Gets the discriminator value for a type (converts PascalCase to snake_case)
    /// </summary>
    private static string GetDiscriminatorValue(Type type)
    {
        // Convert PascalCase to snake_case: "SendEmail" -> "send_email"
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
}