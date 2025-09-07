using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using NJsonSchema.Annotations;
using NUnit.Framework;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkPlayground.Utils;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework.Internal;

namespace SkPlaygroundTests;

public class PolimorphicSchemaTests : SemanticKernelTestBase
{
    /// <summary>
    /// Generates polymorphic JSON schema using the unified PetOwnerManager
    /// </summary>
    /// <typeparam name="T">The root type to generate schema for</typeparam>
    /// <returns>JSON schema string with proper polymorphic support</returns>
    private static string GeneratePolymorphicJsonSchema<T>()
    {
        var manager = new PetOwnerManager()
            .AddDerivedType<Dog>()
            .AddDerivedType<Cat>();
        
        return manager.GenerateSchema();
    }

    [Test]
    public void Contains_derived_references()
    {
        var schema = GeneratePolymorphicJsonSchema<PetOwner>();
        var schemaObj = System.Text.Json.JsonDocument.Parse(schema);
        var root = schemaObj.RootElement;
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions'");
        Assert.That(definitions.TryGetProperty("Dog", out var dog), Is.True, "Schema should have 'Dog' definition");
        Assert.That(definitions.TryGetProperty("Cat", out var cat), Is.True, "Schema should have 'Cat' definition");

        // now verify that dog schema is correct
        Assert.That(dog.TryGetProperty("properties", out var dogProps), Is.True, "Dog schema should have 'properties'");
        var dogPropsObj = dogProps.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        Assert.That(dogPropsObj.ContainsKey("type"), Is.True, "Dog schema should have 'type' property");
        Assert.That(dogPropsObj.ContainsKey("breed"), Is.True, "Dog schema should have 'breed' property");
        Assert.That(dogPropsObj.ContainsKey("barkVolume"), Is.True, "Dog schema should have 'barkVolume' property");
    }

    [Test]
    public void Can_generate_schema_with_only_cat()
    {
        // Test the flexibility of the new PetOwnerManager - only Cat, no Dog
        var manager = new PetOwnerManager().AddDerivedType<Cat>();
        var schemaJson = manager.GenerateSchema();

        var schemaObj = System.Text.Json.JsonDocument.Parse(schemaJson);
        var root = schemaObj.RootElement;

        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True);
        Assert.That(definitions.TryGetProperty("Cat", out _), Is.True, "Should have Cat definition");
        Assert.That(definitions.TryGetProperty("Dog", out _), Is.False, "Should NOT have Dog definition");

        // Verify pet property has anyOf with only Cat
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True);
        Assert.That(properties.TryGetProperty("pet", out var petProp), Is.True);
        Assert.That(petProp.TryGetProperty("anyOf", out var anyOf), Is.True);
        Assert.That(anyOf.GetArrayLength(), Is.EqualTo(1), "Should have exactly one anyOf option (Cat only)");
    }

    [Test]
    public void Derived_class_schema_has_discriminator_type()
    {
        var schema = GeneratePolymorphicJsonSchema<PetOwner>();
        var schemaObj = System.Text.Json.JsonDocument.Parse(schema);
        var root = schemaObj.RootElement;
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True, "Schema should have 'definitions'");
        Assert.That(definitions.TryGetProperty("Dog", out var dog), Is.True, "Schema should have 'Dog' definition");

        // now verify that dog schema has type property with enum "dog"
        Assert.That(dog.TryGetProperty("properties", out var dogProps), Is.True, "Dog schema should have 'properties'");
        var dogPropsObj = dogProps.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        Assert.That(dogPropsObj.ContainsKey("type"), Is.True, "Dog schema should have 'type' property");
        var typeProp = dogPropsObj["type"];

        Assert.That(typeProp.TryGetProperty("enum", out var enumValues), Is.True, "type property should have 'enum'");
        var enums = enumValues.EnumerateArray().Select(v => v.GetString()).ToList();
        Assert.That(enums, Contains.Item("dog"), "type enum should contain 'dog'");

        //should have a title
        Assert.That(typeProp.TryGetProperty("title", out var titleValue), Is.True, "type property should have 'title'");
        Assert.That(titleValue.GetString(), Is.EqualTo("Type"), "type property should have title 'Type'");

        //type of the property should be string
        Assert.That(typeProp.TryGetProperty("type", out var typeType), Is.True, "type property should have 'type'");
        Assert.That(typeType.GetString(), Is.EqualTo("string"), "type property should be of type string");

        //it should also have const equals to dog
        Assert.That(typeProp.TryGetProperty("const", out var constValue), Is.True, "type property should have 'const'");
        Assert.That(constValue.GetString(), Is.EqualTo("dog"), "type const should be 'dog'");
    }

    [Test]
    public void Base_class_Contains_anyof()
    {
        var schema = GeneratePolymorphicJsonSchema<PetOwner>();
        var schemaObj = System.Text.Json.JsonDocument.Parse(schema);
        var root = schemaObj.RootElement;
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True, "Schema should have 'properties'");
        Assert.That(properties.TryGetProperty("pet", out var petProp), Is.True, "Schema should have 'pet' property");

        // Verify pet property has anyOf with Dog and Cat
        Assert.That(petProp.TryGetProperty("anyOf", out var anyOf), Is.True, "pet property should have 'anyOf'");
        var anyOfArray = anyOf.EnumerateArray().ToList();
        Assert.That(anyOfArray.Count, Is.EqualTo(2), "anyOf should have exactly two options (Dog and Cat)");

        // Verify that anyOf contains references to Dog and Cat definitions
        var refValues = anyOfArray
            .Select(v => v.GetProperty("$ref").GetString())
            .ToList();

        Assert.That(refValues, Contains.Item("#/definitions/Dog"), "anyOf should contain reference to Dog");
        Assert.That(refValues, Contains.Item("#/definitions/Cat"), "anyOf should contain reference to Cat");
    }

    [Test]
    public void Can_deserialize_polimorphic()
    {
        var json = @"
        {
            ""name"": ""Jane"",
            ""surname"": ""Doe"",
            ""address"": ""456 Oak Avenue, Metropolis, IL 62960"",
            ""pet"": {
                ""type"": ""dog"",
                ""breed"": ""Labrador Retriever"",
                ""barkVolume"": 7
            }
        }";

        // Use PetOwnerManager for polymorphic deserialization
        var manager = new PetOwnerManager()
            .AddDerivedType<Dog>()
            .AddDerivedType<Cat>();
        var petOwner = manager.DeserializeFromJson(json);
        
        Assert.That(petOwner, Is.Not.Null, "Should deserialize to PetOwner object");
        Assert.That(petOwner.Pet, Is.Not.Null, "Pet should not be null");
        Assert.That(petOwner.Pet, Is.TypeOf<Dog>(), "Pet should deserialize as Dog type");
        var dog = petOwner.Pet as Dog;
        Assert.That(dog, Is.Not.Null, "Should be able to cast pet to Dog");
        Assert.That(dog.Breed, Is.EqualTo("Labrador Retriever"), "Dog breed should match");
        Assert.That(dog.BarkVolume, Is.EqualTo(7), "Dog barkVolume should match");
     }

    [Test]
    public void Can_deserialize_polimorphic_cat()
    {
        var json = @"
        {
            ""name"": ""Michael"",
            ""surname"": ""Brown"",
            ""address"": ""789 Pine Street, Austin, TX 73301"",
            ""pet"": {
                ""type"": ""cat"",
                ""color"": ""orange tabby""
            }
        }";

        // Use PetOwnerManager for polymorphic deserialization
        var manager = new PetOwnerManager()
            .AddDerivedType<Dog>()
            .AddDerivedType<Cat>();
        var petOwner = manager.DeserializeFromJson(json);
        
        Assert.That(petOwner, Is.Not.Null, "Should deserialize to PetOwner object");
        Assert.That(petOwner.Pet, Is.Not.Null, "Pet should not be null");
        Assert.That(petOwner.Pet, Is.TypeOf<Cat>(), "Pet should deserialize as Cat type");
        var cat = petOwner.Pet as Cat;
        Assert.That(cat, Is.Not.Null, "Should be able to cast pet to Cat");
        Assert.That(cat.Color, Is.EqualTo("orange tabby"), "Cat color should match");
    }

    [Test]
    public void PetOwnerManager_demonstrates_flexibility()
    {
        // Create manager with only Dog support
        var dogOnlyManager = new PetOwnerManager().AddDerivedType<Dog>();
        
        // Generate schema - should only include Dog
        var dogSchema = dogOnlyManager.GenerateSchema();
        Assert.That(dogSchema, Contains.Substring("Dog"), "Should contain Dog definition");
        Assert.That(dogSchema, Does.Not.Contain("Cat"), "Should NOT contain Cat definition");
        
        // Test deserialization with Dog JSON
        var dogJson = @"{
            ""name"": ""John"",
            ""surname"": ""Doe"",
            ""address"": ""123 Main St"",
            ""pet"": {
                ""type"": ""dog"",
                ""breed"": ""Labrador"",
                ""barkVolume"": 5
            }
        }";
        
        var dogOwner = dogOnlyManager.DeserializeFromJson(dogJson);
        Assert.That(dogOwner?.Pet, Is.TypeOf<Dog>(), "Should deserialize as Dog");
        
        // Create manager with both Dog and Cat support using AddDerivedTypes
        var fullManager = new PetOwnerManager().AddDerivedTypes(typeof(Dog), typeof(Cat));
        
        // Generate full schema - should include both
        var fullSchema = fullManager.GenerateSchema();
        Assert.That(fullSchema, Contains.Substring("Dog"), "Should contain Dog definition");
        Assert.That(fullSchema, Contains.Substring("Cat"), "Should contain Cat definition");
        
        // Test deserialization with Cat JSON
        var catJson = @"{
            ""name"": ""Jane"",
            ""surname"": ""Smith"",
            ""address"": ""456 Oak Ave"",
            ""pet"": {
                ""type"": ""cat"",
                ""color"": ""black""
            }
        }";
        
        var catOwner = fullManager.DeserializeFromJson(catJson);
        Assert.That(catOwner?.Pet, Is.TypeOf<Cat>(), "Should deserialize as Cat");
    }

    [Test]
    public void GenerateSchema_WithSpecificTypes_OnlyIncludesSpecifiedTypes()
    {
        // Create manager with both Dog and Cat configured
        var manager = new PetOwnerManager()
            .AddDerivedType<Dog>()
            .AddDerivedType<Cat>();
        
        // Generate schema with only Dog
        var dogOnlySchema = manager.GenerateSchema(new[] { typeof(Dog) });
        var schemaObj = System.Text.Json.JsonDocument.Parse(dogOnlySchema);
        var root = schemaObj.RootElement;
        
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True);
        Assert.That(definitions.TryGetProperty("Dog", out _), Is.True, "Should have Dog definition");
        Assert.That(definitions.TryGetProperty("Cat", out _), Is.False, "Should NOT have Cat definition");
        
        // Verify pet property has anyOf with only Dog
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True);
        Assert.That(properties.TryGetProperty("pet", out var petProp), Is.True);
        Assert.That(petProp.TryGetProperty("anyOf", out var anyOf), Is.True);
        Assert.That(anyOf.GetArrayLength(), Is.EqualTo(1), "Should have exactly one anyOf option (Dog only)");
        
        var refValue = anyOf.EnumerateArray().First().GetProperty("$ref").GetString();
        Assert.That(refValue, Is.EqualTo("#/definitions/Dog"), "anyOf should contain reference to Dog only");
    }

    [Test]
    public void GenerateSchema_WithSpecificTypes_CanIncludeCatOnly()
    {
        // Create manager with both Dog and Cat configured
        var manager = new PetOwnerManager()
            .AddDerivedType<Dog>()
            .AddDerivedType<Cat>();
        
        // Generate schema with only Cat
        var catOnlySchema = manager.GenerateSchema(new[] { typeof(Cat) });
        var schemaObj = System.Text.Json.JsonDocument.Parse(catOnlySchema);
        var root = schemaObj.RootElement;
        
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True);
        Assert.That(definitions.TryGetProperty("Cat", out _), Is.True, "Should have Cat definition");
        Assert.That(definitions.TryGetProperty("Dog", out _), Is.False, "Should NOT have Dog definition");
        
        // Verify pet property has anyOf with only Cat
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True);
        Assert.That(properties.TryGetProperty("pet", out var petProp), Is.True);
        Assert.That(petProp.TryGetProperty("anyOf", out var anyOf), Is.True);
        Assert.That(anyOf.GetArrayLength(), Is.EqualTo(1), "Should have exactly one anyOf option (Cat only)");
        
        var refValue = anyOf.EnumerateArray().First().GetProperty("$ref").GetString();
        Assert.That(refValue, Is.EqualTo("#/definitions/Cat"), "anyOf should contain reference to Cat only");
    }

    [Test]
    public void GenerateSchema_WithSpecificTypes_CanIncludeBothTypes()
    {
        // Create manager with both Dog and Cat configured
        var manager = new PetOwnerManager()
            .AddDerivedType<Dog>()
            .AddDerivedType<Cat>();
        
        // Generate schema with both types explicitly specified
        var fullSchema = manager.GenerateSchema(new[] { typeof(Dog), typeof(Cat) });
        var schemaObj = System.Text.Json.JsonDocument.Parse(fullSchema);
        var root = schemaObj.RootElement;
        
        Assert.That(root.TryGetProperty("definitions", out var definitions), Is.True);
        Assert.That(definitions.TryGetProperty("Dog", out _), Is.True, "Should have Dog definition");
        Assert.That(definitions.TryGetProperty("Cat", out _), Is.True, "Should have Cat definition");
        
        // Verify pet property has anyOf with both Dog and Cat
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True);
        Assert.That(properties.TryGetProperty("pet", out var petProp), Is.True);
        Assert.That(petProp.TryGetProperty("anyOf", out var anyOf), Is.True);
        Assert.That(anyOf.GetArrayLength(), Is.EqualTo(2), "Should have exactly two anyOf options");
        
        var refValues = anyOf.EnumerateArray()
            .Select(v => v.GetProperty("$ref").GetString())
            .ToList();
        
        Assert.That(refValues, Contains.Item("#/definitions/Dog"), "anyOf should contain reference to Dog");
        Assert.That(refValues, Contains.Item("#/definitions/Cat"), "anyOf should contain reference to Cat");
    }

    [Test]
    public void GenerateSchema_WithUnconfiguredType_ThrowsException()
    {
        // Create manager with only Dog configured
        var manager = new PetOwnerManager().AddDerivedType<Dog>();
        
        // Try to generate schema with Cat (not configured)
        Assert.Throws<ArgumentException>(() => 
            manager.GenerateSchema(new[] { typeof(Cat) }),
            "Should throw ArgumentException when trying to include unconfigured type");
    }

    [Test]
    public void GenerateSchema_WithEmptyTypesList_ThrowsException()
    {
        var manager = new PetOwnerManager()
            .AddDerivedType<Dog>()
            .AddDerivedType<Cat>();
        
        Assert.Throws<ArgumentException>(() => 
            manager.GenerateSchema(new Type[0]),
            "Should throw ArgumentException when no types are specified");
    }

    [Test]
    public void GenerateSchema_WithSpecificTypes_DeserializationStillWorksForAllConfiguredTypes()
    {
        // Create manager with both types configured
        var manager = new PetOwnerManager()
            .AddDerivedType<Dog>()
            .AddDerivedType<Cat>();
        
        // Generate schema with only Dog
        var dogOnlySchema = manager.GenerateSchema(new[] { typeof(Dog) });
        Assert.That(dogOnlySchema, Contains.Substring("Dog"), "Schema should contain Dog");
        Assert.That(dogOnlySchema, Does.Not.Contain("Cat"), "Schema should NOT contain Cat");
        
        // But deserialization should still work for both types since both are configured
        var dogJson = @"{
            ""name"": ""John"",
            ""surname"": ""Doe"",
            ""address"": ""123 Main St"",
            ""pet"": {
                ""type"": ""dog"",
                ""breed"": ""Labrador"",
                ""barkVolume"": 5
            }
        }";
        
        var catJson = @"{
            ""name"": ""Jane"",
            ""surname"": ""Smith"",
            ""address"": ""456 Oak Ave"",
            ""pet"": {
                ""type"": ""cat"",
                ""color"": ""black""
            }
        }";
        
        var dogOwner = manager.DeserializeFromJson(dogJson);
        var catOwner = manager.DeserializeFromJson(catJson);
        
        Assert.That(dogOwner?.Pet, Is.TypeOf<Dog>(), "Should still deserialize Dog");
        Assert.That(catOwner?.Pet, Is.TypeOf<Cat>(), "Should still deserialize Cat even though it wasn't in the schema");
    }

    [Test]
    public void SchemaCaching_GeneratesConsistentSchemas()
    {
        // Create manager and add types
        var manager = new PetOwnerManager()
            .AddDerivedType<Dog>()
            .AddDerivedType<Cat>();
        
        // Generate schema multiple times - should be identical due to caching
        var schema1 = manager.GenerateSchema();
        var schema2 = manager.GenerateSchema();
        var schema3 = manager.GenerateSchema(new[] { typeof(Dog), typeof(Cat) });
        
        Assert.That(schema1, Is.EqualTo(schema2), "Multiple calls to GenerateSchema() should return identical results");
        Assert.That(schema1, Is.EqualTo(schema3), "GenerateSchema() and GenerateSchema(allTypes) should return identical results");
    }

    [Test]
    public void SchemaCaching_WorksWithSelectiveGeneration()
    {
        // Create manager and add types
        var manager = new PetOwnerManager()
            .AddDerivedType<Dog>()
            .AddDerivedType<Cat>();
        
        // Generate selective schemas multiple times
        var dogSchema1 = manager.GenerateSchema(new[] { typeof(Dog) });
        var dogSchema2 = manager.GenerateSchema(new[] { typeof(Dog) });
        var catSchema1 = manager.GenerateSchema(new[] { typeof(Cat) });
        var catSchema2 = manager.GenerateSchema(new[] { typeof(Cat) });
        
        Assert.That(dogSchema1, Is.EqualTo(dogSchema2), "Multiple Dog-only schema generations should be identical");
        Assert.That(catSchema1, Is.EqualTo(catSchema2), "Multiple Cat-only schema generations should be identical");
        Assert.That(dogSchema1, Is.Not.EqualTo(catSchema1), "Dog-only and Cat-only schemas should be different");
    }

    [Test]
    public void SchemaCaching_AddingTypesInDifferentOrder_ProducesSameResult()
    {
        // Create two managers with types added in different orders
        var manager1 = new PetOwnerManager()
            .AddDerivedType<Dog>()
            .AddDerivedType<Cat>();
            
        var manager2 = new PetOwnerManager()
            .AddDerivedType<Cat>()
            .AddDerivedType<Dog>();
        
        var schema1 = manager1.GenerateSchema();
        var schema2 = manager2.GenerateSchema();
        
        // Parse and compare the schemas structurally since property order might differ
        var schemaObj1 = System.Text.Json.JsonDocument.Parse(schema1);
        var schemaObj2 = System.Text.Json.JsonDocument.Parse(schema2);
        
        // Both should have the same definitions
        Assert.That(schemaObj1.RootElement.TryGetProperty("definitions", out var defs1), Is.True);
        Assert.That(schemaObj2.RootElement.TryGetProperty("definitions", out var defs2), Is.True);
        
        Assert.That(defs1.TryGetProperty("Dog", out _), Is.True, "Manager1 should have Dog definition");
        Assert.That(defs1.TryGetProperty("Cat", out _), Is.True, "Manager1 should have Cat definition");
        Assert.That(defs2.TryGetProperty("Dog", out _), Is.True, "Manager2 should have Dog definition");
        Assert.That(defs2.TryGetProperty("Cat", out _), Is.True, "Manager2 should have Cat definition");
    }

    // [Test]
    // public async Task GenerateJsonSchema_RealLLMCall_PolymorphicCatOwner()

    //     {
    //         // Skip test if no API key is available
    //         var apiKey = Dotenv.Get("OPENAI_API_KEY");
    //         var endpoint = Dotenv.Get("AZURE_ENDPOINT");

    //         if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint))
    //         {
    //             Assert.Ignore("OPENAI_API_KEY or AZURE_ENDPOINT environment variable not set");
    //             return;
    //         }

    //         // Generate the JSON schema for PetOwner class using polymorphic schema generation
    //         var schemaJson = GeneratePolymorphicJsonSchema<PetOwner>();
    //         IChatCompletionService chatService = GetCompletionService(apiKey, endpoint, schemaJson);

    //         var userPrompt = @"Please format this pet owner data into JSON:
    // Michael Brown lives at 789 Pine Street, Austin, TX 73301. He owns a beautiful orange tabby cat named Whiskers.";

    //         var chatHistory = new ChatHistory();
    //         chatHistory.AddUserMessage(userPrompt);

    //         var chatResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
    //             jsonSchemaFormatName: "pet_owner",
    //             jsonSchema: BinaryData.FromString(schemaJson),
    //             jsonSchemaIsStrict: true
    //         );
    //         var executionSettings = new OpenAIPromptExecutionSettings
    //         {
    //             ResponseFormat = chatResponseFormat
    //         };

    //         Console.WriteLine("\nCalling LLM to reformat cat owner data...");


    //         // Test deserialization into our PetOwner class with polymorphic Pet
    //         try
    //         {
    //             // Make the LLM call
    //             var skResponse = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings);
    //             var jsonResponse = skResponse.Content;

    //             Console.WriteLine("\nLLM Response:");
    //             Console.WriteLine(jsonResponse);

    //             var petOwner = JsonConvert.DeserializeObject<PetOwner>(jsonResponse);

    //             Assert.That(petOwner, Is.Not.Null, "Should deserialize to PetOwner object");
    //             Assert.That(petOwner.Pet, Is.Not.Null, "Pet should not be null");

    //             // Verify polymorphic deserialization - should be a Cat
    //             Assert.That(petOwner.Pet, Is.TypeOf<Cat>(), "Pet should deserialize as Cat type");

    //             var cat = petOwner.Pet as Cat;
    //             Assert.That(cat, Is.Not.Null, "Should be able to cast pet to Cat");
    //             Assert.That(cat.Color, Is.Not.Null.And.Not.Empty, "Cat color should not be empty");
    //             Assert.That(cat.Color.ToLower(), Contains.Substring("orange").Or.Contains("tabby"), "Should extract orange/tabby color");

    //             Console.WriteLine($"✅ Successfully extracted: {petOwner.Name} {petOwner.Surname} with {cat.Color} cat");
    //             Console.WriteLine("✅ Polymorphic deserialization successful - Pet correctly identified as Cat");
    //         }
    //         catch (Exception ex)
    //         {

    //             Assert.Fail($"Failed to perform CALL: {ex.Message} - {schemaJson}");
    //         }

    //         Console.WriteLine("✅ Real LLM call with polymorphic Cat schema validation completed successfully!");
    //     }
}

public class PetOwner
{
    [JsonProperty("name")]
    [Required]
    public string Name { get; set; } = default!;

    [JsonProperty("surname")]
    [Required]
    public string Surname { get; set; } = default!;

    [JsonProperty("address")]
    [Required]
    public string Address { get; set; } = default!;

    [JsonProperty("pet")]
    [Required]
    public Pet Pet { get; set; } = default!;
}

/// <summary>
/// Abstract base class for pets with JSON polymorphic support
/// </summary>
public abstract class Pet
{
    /// <summary>
    /// Discriminator property to identify the pet type
    /// </summary>
    [JsonProperty("type")]
    [Required]
    public abstract string Type { get; }
}

/// <summary>
/// Represents a dog with breed and bark volume characteristics
/// </summary>
public class Dog : Pet
{
    /// <summary>
    /// Type discriminator for polymorphic deserialization
    /// </summary>
    public override string Type => "dog";

    /// <summary>
    /// The breed of the dog
    /// </summary>
    [JsonProperty("breed")]
    [Required]
    public string Breed { get; set; } = default!;

    /// <summary>
    /// The volume level of the dog's bark (1-10 scale)
    /// </summary>
    [JsonProperty("barkVolume")]
    [Required]
    public int BarkVolume { get; set; }
}

/// <summary>
/// Represents a cat with color characteristics
/// </summary>
public class Cat : Pet
{
    /// <summary>
    /// Type discriminator for polymorphic deserialization
    /// </summary>
    public override string Type => "cat";

    /// <summary>
    /// The color/pattern of the cat (e.g., "orange tabby", "black", "calico")
    /// </summary>
    [JsonProperty("color")]
    [Required]
    public string Color { get; set; } = default!;
}