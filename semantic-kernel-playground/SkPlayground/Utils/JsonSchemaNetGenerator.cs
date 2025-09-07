// using System;

// namespace SkPlayground.Utils;

// using System.ComponentModel;
// using System.ComponentModel.DataAnnotations;
// using System.Reflection;
// using System.Text.Json;
// using System.Text.Json.Serialization;
// using Json.Schema;
// using Json.Schema.Generation;
// using Json.Schema.Generation.Generators;
// using Json.Schema.Generation.Intents;

// public class JsonSchemaNetGenerator
// {
//     private readonly SchemaGeneratorConfiguration _config;
    
//     public JsonSchemaNetGenerator()
//     {
//         _config = new SchemaGeneratorConfiguration();
        
//         // Configure to use descriptions from attributes
//         _config.Generators.Add(new DescriptionAttributeSchemaGenerator());
        
//         // Configure to handle polymorphic types
//         _config.Generators.Add(new PolymorphicSchemaGenerator());
        
//         // Use camelCase for property names
//         _config.PropertyNamingMethod = PropertyNamingMethods.CamelCase;
        
//         // Set default values
//         _config.Generators.Add(new DefaultValueSchemaGenerator());
//     }
    
//     public JsonSchema GenerateSchema<T>()
//     {
//         return GenerateSchema(typeof(T));
//     }
    
//     public JsonSchema GenerateSchema(Type type)
//     {
//         return _config.GetJsonSchema(type, EvaluationOptions.From(SpecVersion.Draft7));
//     }
    
//     public string GenerateSchemaAsString<T>()
//     {
//         var schema = GenerateSchema<T>();
//         return JsonSerializer.Serialize(schema, new JsonSerializerOptions 
//         { 
//             WriteIndented = true,
//             PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//         });
//     }
// }

// // Custom schema generator for Description attributes
// public class DescriptionAttributeSchemaGenerator : ISchemaGenerator
// {
//     public bool Handles(Type type) => true;

//     public void AddConstraints(SchemaGeneratorContext context)
//     {
//         var descriptionAttr = context.Type.GetCustomAttribute<DescriptionAttribute>() ??
//                              context.MemberInfo?.GetCustomAttribute<DescriptionAttribute>();
        
//         if (descriptionAttr != null)
//         {
//             context.Intents.Add(new DescriptionIntent(descriptionAttr.Description));
//         }
//     }
// }

// // Custom schema generator for default values
// public class DefaultValueSchemaGenerator : ISchemaGenerator
// {
//     public bool Handles(Type type) => true;

//     public void AddConstraints(SchemaGeneratorContext context)
//     {
//         if (context.MemberInfo is PropertyInfo property)
//         {
//             var defaultValueAttr = property.GetCustomAttribute<DefaultValueAttribute>();
//             if (defaultValueAttr != null)
//             {
//                 context.Intents.Add(new DefaultIntent(JsonSerializer.SerializeToElement(defaultValueAttr.Value)));
//             }
//         }
//     }
// }

// // Custom schema generator for polymorphic types
// public class PolymorphicSchemaGenerator : ISchemaGenerator
// {
//     public bool Handles(Type type)
//     {
//         return type.GetCustomAttribute<JsonPolymorphicAttribute>() != null;
//     }

//     public void AddConstraints(SchemaGeneratorContext context)
//     {
//         if (!Handles(context.Type)) return;

//         var polymorphicAttr = context.Type.GetCustomAttribute<JsonPolymorphicAttribute>();
//         var derivedTypeAttrs = context.Type.GetCustomAttributes<JsonDerivedTypeAttribute>().ToList();

//         if (!derivedTypeAttrs.Any()) return;

//         var anyOfSchemas = new List<JsonSchema>();

//         foreach (var derivedAttr in derivedTypeAttrs)
//         {
//             var derivedType = derivedAttr.DerivedType;
//             var discriminator = derivedAttr.TypeDiscriminator?.ToString() ?? derivedType.Name;
            
//             // Generate schema for the derived type
//             var derivedSchema = context.Configuration.GetJsonSchema(derivedType, context.Options);
            
//             // Create a new schema builder to modify the derived schema
//             var builder = new JsonSchemaBuilder()
//                 .Type(SchemaValueType.Object);

//             // Copy properties from the derived schema
//             if (derivedSchema.GetProperties() != null)
//             {
//                 foreach (var prop in derivedSchema.GetProperties())
//                 {
//                     builder.Properties(prop);
//                 }
//             }

//             // Add the discriminator property
//             var discriminatorPropName = polymorphicAttr?.TypeDiscriminatorPropertyName ?? "$type";
//             builder.Properties((discriminatorPropName, new JsonSchemaBuilder()
//                 .Type(SchemaValueType.String)
//                 .Const(discriminator)
//                 .Build()));

//             // Copy required properties and add discriminator as required
//             var requiredProps = new List<string>();
//             if (derivedSchema.GetRequired() != null)
//             {
//                 requiredProps.AddRange(derivedSchema.GetRequired());
//             }
//             requiredProps.Add(discriminatorPropName);
//             builder.Required(requiredProps);

//             anyOfSchemas.Add(builder.Build());
//         }

//         // Replace the current schema with an anyOf schema
//         context.Intents.Clear();
//         context.Intents.Add(new AnyOfIntent(anyOfSchemas));
//     }
// }
