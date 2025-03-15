using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using System.Diagnostics;

namespace LocalCallRedirection
{
    // Custom HttpClientHandler to redirect OpenAI API calls to local LLM server
    public class ProxyOpenAIHandler : HttpClientHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri != null && request.RequestUri.Host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase))
            {
                // Redirect to your local LLM server
                request.RequestUri = new Uri($"http://localhost:1234{request.RequestUri.PathAndQuery}");
            }
            return base.SendAsync(request, cancellationToken);
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting local call redirection demo...");

            // Configure kernel with custom HttpClient for local redirection
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddLogging(l => l
                .SetMinimumLevel(LogLevel.Warning)
                .AddConsole()
                .AddDebug()
            );

            // Create custom HttpClient with our proxy handler
            var httpClient = new HttpClient(new ProxyOpenAIHandler())
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

            // Add OpenAI chat completion service with our custom httpClient
            kernelBuilder.AddOpenAIChatCompletion("xxx", "xxx", httpClient: httpClient);
            var kernel = kernelBuilder.Build();
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            // Simple prompt example
            Console.WriteLine("\nRunning simple prompt example:");
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var simpleCallResult = await kernel.InvokePromptAsync("Which is the capital of France. Answer as pirate Barbossa!!!");
            stopwatch.Stop();
            Console.WriteLine($"Time taken for simple prompt: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine(simpleCallResult);
            Console.WriteLine();

            // Image path - update this if needed
            var imagePath = "/Users/gianmariaricci/Desktop/test.png";

            if (!File.Exists(imagePath))
            {
                Console.WriteLine($"Warning: Image file not found at {imagePath}");
                Console.WriteLine("Please update the path to a valid image to run the image examples.");
                return;
            }

            // Simple image example without function calling
            try
            {
                Console.WriteLine("\nRunning simple image example (no function calling):");

                var oaiSettings = new OpenAIPromptExecutionSettings()
                {
                    MaxTokens = 1000,
                    Temperature = 0,
                };

                ChatHistory chatMessages = new();
                chatMessages.AddSystemMessage("you are an expert technician that will extract information from images");

                var bytes = File.ReadAllBytes(imagePath);
                var imageData = new ReadOnlyMemory<byte>(bytes);
                var message = new ChatMessageContentItemCollection
                    {
                        new TextContent(@"This is a page of a pdf document that represents a technical manual, please describe the page, identify images and generate a markdown that explain with great detail what is in this page.."),
                        new Microsoft.SemanticKernel.ImageContent(imageData, "image/png")
                    };

                chatMessages.AddUserMessage(message);
                stopwatch.Restart();
                var result = await chatCompletionService.GetChatMessageContentAsync(chatMessages, oaiSettings);
                stopwatch.Stop();
                Console.WriteLine($"Time taken for image processing: {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in simple image example: {ex.Message}");
            }
        }
    }
}
