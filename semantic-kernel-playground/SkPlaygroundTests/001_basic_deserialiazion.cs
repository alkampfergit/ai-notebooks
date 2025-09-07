using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
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

[TestFixture]
public class BasicSchemaTests : SemanticKernelTestBase
{
    [Test]
    public void GenerateJsonSchema_Person()
    {
        var schemaJson = GenerateJsonSchema<Person>();

        Console.WriteLine("Generated Schema:");
        Console.WriteLine(schemaJson);

        // Parse the JSON to validate structure
        var schemaObj = System.Text.Json.JsonDocument.Parse(schemaJson);
        var root = schemaObj.RootElement;

        // Verify top-level schema structure
        Assert.That(root.TryGetProperty("type", out var typeProperty), Is.True, "Schema should have 'type' property");
        Assert.That(typeProperty.GetString(), Is.EqualTo("object"), "Root type should be 'object'");
        Assert.That(root.TryGetProperty("title", out var titleProperty), Is.True, "Schema should have 'title' property");
        Assert.That(titleProperty.GetString(), Is.EqualTo("Person"), "Title should be 'Person'");

        // Verify properties exist
        Assert.That(root.TryGetProperty("properties", out var properties), Is.True, "Schema should have 'properties'");
        Assert.That(properties.TryGetProperty("name", out _), Is.True, "Should have 'name' property");
        Assert.That(properties.TryGetProperty("surname", out _), Is.True, "Should have 'surname' property");
        Assert.That(properties.TryGetProperty("address", out _), Is.True, "Should have 'address' property");

        // Verify required fields
        Assert.That(root.TryGetProperty("required", out var requiredArray), Is.True, "Schema should have 'required' array");
        var requiredFields = new HashSet<string>();
        foreach (var item in requiredArray.EnumerateArray())
        {
            requiredFields.Add(item.GetString());
        }
        Assert.That(requiredFields, Contains.Item("name"), "name should be required");
        Assert.That(requiredFields, Contains.Item("surname"), "surname should be required");
        Assert.That(requiredFields, Contains.Item("address"), "address should be required");

        // Verify property types
        Assert.That(properties.TryGetProperty("name", out var nameProp), Is.True);
        Assert.That(nameProp.TryGetProperty("type", out var nameType), Is.True);
        Assert.That(nameType.GetString(), Is.EqualTo("string"), "name should be string type");

        Assert.That(properties.TryGetProperty("surname", out var surnameProp), Is.True);
        Assert.That(surnameProp.TryGetProperty("type", out var surnameType), Is.True);
        Assert.That(surnameType.GetString(), Is.EqualTo("string"), "surname should be string type");

        Assert.That(properties.TryGetProperty("address", out var addressProp), Is.True);
        Assert.That(addressProp.TryGetProperty("type", out var addressType), Is.True);
        Assert.That(addressType.GetString(), Is.EqualTo("string"), "address should be string type");

        Console.WriteLine("✅ Schema structure validation passed!");
        Console.WriteLine($"✅ Found {requiredFields.Count} required fields");
    }

    [Test]
    public async Task GenerateJsonSchema_RealLLMCall_ReformatPersonData()
    {
        // Skip test if no API key is available
        var apiKey = Dotenv.Get("OPENAI_API_KEY");
        var endpoint = Dotenv.Get("AZURE_ENDPOINT");

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint))
        {
            Assert.Ignore("OPENAI_API_KEY or AZURE_ENDPOINT environment variable not set");
            return;
        }

        // Generate the JSON schema for Person class
        var schemaJson = GenerateJsonSchema<Person>();
        IChatCompletionService chatService = GetCompletionService(apiKey, endpoint, schemaJson);

        var userPrompt = @"Please format this person data into JSON:
John Smith lives at 123 Main Street, Springfield, IL 62701. He is a software engineer and has been working remotely since 2020.";

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(userPrompt);

        var chatResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: "product_review",
            jsonSchema: BinaryData.FromString(schemaJson),
            jsonSchemaIsStrict: true
        );
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = chatResponseFormat
        };

        Console.WriteLine("\nCalling LLM to reformat person data...");

        // Make the LLM call
        var skResponse = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings);
        var jsonResponse = skResponse.Content;

        Console.WriteLine("\nLLM Response:");
        Console.WriteLine(jsonResponse);

        // Validate that the response is valid JSON using Newtonsoft.Json
        Assert.That(jsonResponse, Is.Not.Null.And.Not.Empty, "LLM should return non-empty response");

        JObject responseObj;
        try
        {
            responseObj = JObject.Parse(jsonResponse);
        }
        catch (JsonReaderException ex)
        {
            Assert.Fail($"LLM response is not valid JSON: {ex.Message}");
            return;
        }

        // Validate the structure matches our Person schema using Newtonsoft.Json
        Assert.That(responseObj.Type, Is.EqualTo(JTokenType.Object), "Response should be a JSON object");

        // Check required properties exist
        Assert.That(responseObj["name"], Is.Not.Null, "Response should have 'name' property");
        Assert.That(responseObj["surname"], Is.Not.Null, "Response should have 'surname' property");
        Assert.That(responseObj["address"], Is.Not.Null, "Response should have 'address' property");

        // Validate property types
        Assert.That(responseObj["name"].Type, Is.EqualTo(JTokenType.String), "name should be a string");
        Assert.That(responseObj["surname"].Type, Is.EqualTo(JTokenType.String), "surname should be a string");
        Assert.That(responseObj["address"].Type, Is.EqualTo(JTokenType.String), "address should be a string");

        // Validate extracted content makes sense
        var name = responseObj["name"].Value<string>();
        var surname = responseObj["surname"].Value<string>();
        var address = responseObj["address"].Value<string>();

        Assert.That(name, Is.Not.Null.And.Not.Empty, "name should not be empty");
        Assert.That(surname, Is.Not.Null.And.Not.Empty, "surname should not be empty");
        Assert.That(address, Is.Not.Null.And.Not.Empty, "address should not be empty");

        // Verify the LLM extracted the correct information
        Assert.That(name.ToLower(), Contains.Substring("john"), "Should extract 'John' as name");
        Assert.That(surname.ToLower(), Contains.Substring("smith"), "Should extract 'Smith' as surname");
        Assert.That(address.ToLower(), Contains.Substring("main"), "Should extract address containing 'Main'");

        Console.WriteLine($"✅ Successfully extracted: {name} {surname} at {address}");

        // Test deserialization into our Person class using Newtonsoft.Json
        try
        {
            var person = JsonConvert.DeserializeObject<Person>(jsonResponse);

            Assert.That(person, Is.Not.Null, "Should deserialize to Person object");
            Assert.That(person.Name, Is.EqualTo(name), "Deserialized name should match");
            Assert.That(person.Surname, Is.EqualTo(surname), "Deserialized surname should match");
            Assert.That(person.Address, Is.EqualTo(address), "Deserialized address should match");

            Console.WriteLine("✅ Successfully deserialized to Person object");
        }
        catch (JsonException ex)
        {
            Assert.Fail($"Failed to deserialize LLM response to Person class: {ex.Message}");
        }

        Console.WriteLine("✅ Real LLM call with schema validation completed successfully!");
    }

    private static IChatCompletionService GetCompletionService(string apiKey, string endpoint, string schemaJson)
    {
        Console.WriteLine("Generated JSON Schema:");
        Console.WriteLine(schemaJson);

        // Initialize Semantic Kernel using the same configuration as Program.cs
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(l => l
            .SetMinimumLevel(LogLevel.Warning)
            .AddConsole()
        );

        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        // Configure Azure OpenAI connection like in Program.cs
        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: "gpt4omini",
            apiKey: apiKey,
            endpoint: endpoint
        );

        var kernel = kernelBuilder.Build();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        return chatService;
    }
}

public class Person
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
}

