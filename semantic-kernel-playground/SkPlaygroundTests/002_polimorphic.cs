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

namespace SkPlaygroundTests;

public class PolimorphicSchemaTests : SemanticKernelTestBase
{
    /// <summary>
    /// Custom schema generation method that properly handles polymorphic Pet types
    /// by configuring the schema generator to include derived types with oneOf constraint
    /// </summary>
    /// <typeparam name="T">The root type to generate schema for</typeparam>
    /// <returns>JSON schema string with proper polymorphic support</returns>
    private static string GeneratePolymorphicJsonSchema<T>()
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

        // Generate the main schema first
        var schema = generator.Generate(typeof(T));

        // Configure polymorphic pet property directly (OpenAI pattern)
        if (schema.Properties.ContainsKey("pet"))
        {
            var petProperty = schema.Properties["pet"];

            // Generate Dog and Cat schemas and flatten them (remove allOf inheritance patterns)
            var dogSchema = generator.Generate(typeof(Dog));
            var catSchema = generator.Generate(typeof(Cat));

            // Extract the actual Dog and Cat definitions and flatten them
            var dogDef = dogSchema.Definitions.ContainsKey("Dog") ? dogSchema.Definitions["Dog"] : dogSchema;
            var catDef = catSchema.Definitions.ContainsKey("Cat") ? catSchema.Definitions["Cat"] : catSchema;
            
            // Flatten Dog schema - remove allOf and merge properties
            var flattenedDog = new JsonSchema
            {
                Type = JsonObjectType.Object,
                AllowAdditionalProperties = false
            };
            
            // Add properties from allOf sections if they exist
            if (dogDef.AllOf.Any())
            {
                foreach (var allOfItem in dogDef.AllOf)
                {
                    foreach (var prop in allOfItem.Properties)
                    {
                        flattenedDog.Properties[prop.Key] = prop.Value;
                    }
                    foreach (var req in allOfItem.RequiredProperties)
                    {
                        flattenedDog.RequiredProperties.Add(req);
                    }
                }
            }
            else
            {
                // If no allOf, copy properties directly
                foreach (var prop in dogDef.Properties)
                {
                    flattenedDog.Properties[prop.Key] = prop.Value;
                }
                foreach (var req in dogDef.RequiredProperties)
                {
                    flattenedDog.RequiredProperties.Add(req);
                }
            }
            
            // Flatten Cat schema - remove allOf and merge properties
            var flattenedCat = new JsonSchema
            {
                Type = JsonObjectType.Object,
                AllowAdditionalProperties = false
            };
            
            // Add properties from allOf sections if they exist
            if (catDef.AllOf.Any())
            {
                foreach (var allOfItem in catDef.AllOf)
                {
                    foreach (var prop in allOfItem.Properties)
                    {
                        flattenedCat.Properties[prop.Key] = prop.Value;
                    }
                    foreach (var req in allOfItem.RequiredProperties)
                    {
                        flattenedCat.RequiredProperties.Add(req);
                    }
                }
            }
            else
            {
                // If no allOf, copy properties directly
                foreach (var prop in catDef.Properties)
                {
                    flattenedCat.Properties[prop.Key] = prop.Value;
                }
                foreach (var req in catDef.RequiredProperties)
                {
                    flattenedCat.RequiredProperties.Add(req);
                }
            }

            // Ensure 'type' is required in both Dog and Cat schemas
            if (!flattenedDog.RequiredProperties.Contains("type"))
            {
                flattenedDog.RequiredProperties.Add("type");
            }
            if (!flattenedCat.RequiredProperties.Contains("type"))
            {
                flattenedCat.RequiredProperties.Add("type");
            }

            // Add const/enum discriminators to type properties (OpenAI pattern)
            if (flattenedDog.Properties.ContainsKey("type"))
            {
                var dogTypeProperty = flattenedDog.Properties["type"];
                dogTypeProperty.Enumeration.Clear();
                dogTypeProperty.Enumeration.Add("dog");
            }
            
            if (flattenedCat.Properties.ContainsKey("type"))
            {
                var catTypeProperty = flattenedCat.Properties["type"];
                catTypeProperty.Enumeration.Clear();
                catTypeProperty.Enumeration.Add("cat");
            }

            // Add flattened schemas to definitions
            schema.Definitions["Dog"] = flattenedDog;
            schema.Definitions["Cat"] = flattenedCat;

            // Replace the pet property reference with direct anyOf constraint (OpenAI pattern)
            petProperty.Reference = null;
            petProperty.AnyOf.Clear();

            // Add direct references to Dog and Cat definitions
            petProperty.AnyOf.Add(new JsonSchema
            {
                Reference = schema.Definitions["Dog"]
            });

            petProperty.AnyOf.Add(new JsonSchema
            {
                Reference = schema.Definitions["Cat"]
            });
        }

        return schema.ToJson();
    }

    [Test]
    public async Task GenerateJsonSchema_RealLLMCall_PolymorphicCatOwner()
    {
        // Skip test if no API key is available
        var apiKey = Dotenv.Get("OPENAI_API_KEY");
        var endpoint = Dotenv.Get("AZURE_ENDPOINT");

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint))
        {
            Assert.Ignore("OPENAI_API_KEY or AZURE_ENDPOINT environment variable not set");
            return;
        }

        // Generate the JSON schema for PetOwner class using polymorphic schema generation
        var schemaJson = GeneratePolymorphicJsonSchema<PetOwner>();
        IChatCompletionService chatService = GetCompletionService(apiKey, endpoint, schemaJson);

        var userPrompt = @"Please format this pet owner data into JSON:
Michael Brown lives at 789 Pine Street, Austin, TX 73301. He owns a beautiful orange tabby cat named Whiskers.";

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(userPrompt);

        var chatResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "pet_owner",
            jsonSchema: BinaryData.FromString(schemaJson),
            jsonSchemaIsStrict: true
        );
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = chatResponseFormat
        };

        Console.WriteLine("\nCalling LLM to reformat cat owner data...");


        // Test deserialization into our PetOwner class with polymorphic Pet
        try
        {
            // Make the LLM call
            var skResponse = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings);
            var jsonResponse = skResponse.Content;

            Console.WriteLine("\nLLM Response:");
            Console.WriteLine(jsonResponse);

            var petOwner = JsonConvert.DeserializeObject<PetOwner>(jsonResponse);

            Assert.That(petOwner, Is.Not.Null, "Should deserialize to PetOwner object");
            Assert.That(petOwner.Pet, Is.Not.Null, "Pet should not be null");

            // Verify polymorphic deserialization - should be a Cat
            Assert.That(petOwner.Pet, Is.TypeOf<Cat>(), "Pet should deserialize as Cat type");

            var cat = petOwner.Pet as Cat;
            Assert.That(cat, Is.Not.Null, "Should be able to cast pet to Cat");
            Assert.That(cat.Color, Is.Not.Null.And.Not.Empty, "Cat color should not be empty");
            Assert.That(cat.Color.ToLower(), Contains.Substring("orange").Or.Contains("tabby"), "Should extract orange/tabby color");

            Console.WriteLine($"✅ Successfully extracted: {petOwner.Name} {petOwner.Surname} with {cat.Color} cat");
            Console.WriteLine("✅ Polymorphic deserialization successful - Pet correctly identified as Cat");
        }
        catch (Exception ex)
        {

            Assert.Fail($"Failed to deserialize LLM response to PetOwner class: {ex.Message}");
        }

        Console.WriteLine("✅ Real LLM call with polymorphic Cat schema validation completed successfully!");
    }
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