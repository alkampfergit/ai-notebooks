using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SkPlayground.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SkPlaygroundTests;

public abstract class SemanticKernelTestBase
{
    /// <summary>
    /// Generates JSON schema for the specified type using NJsonSchema with consistent settings.
    /// </summary>
    /// <typeparam name="T">The type to generate schema for</typeparam>
    /// <returns>JSON schema as string</returns>
    protected static string GenerateJsonSchema<T>()
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
        var schema = generator.Generate(typeof(T));
        return schema.ToJson();
    }

    protected static IChatCompletionService GetCompletionService(string apiKey, string endpoint, string schemaJson)
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
            deploymentName: "gpt-4o-mini",
            apiKey: apiKey,
            endpoint: endpoint
        );

        var kernel = kernelBuilder.Build();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        return chatService;
    }
}
