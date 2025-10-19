using NUnit.Framework;
using SkPlayground.Utils;
using SkPlayground.BusinessFunctions;
using Azure.AI.OpenAI;
 using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenAI.Responses;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Spectre.Console;
using OpenAI.Containers;

namespace SkPlaygroundTests;

#pragma warning disable OPENAI001

/// <summary>
/// **Tests for OpenAI Response API integration**
///
/// This test class validates the new Azure OpenAI Response API functionality.
/// The Response API is a newer, more direct way to interact with OpenAI models
/// without the Semantic Kernel abstraction layer.
///
/// Key features of the Response API:
/// - **Direct API Access**: No Semantic Kernel overhead
/// - **Reasoning Capabilities**: Built-in support for extended reasoning with tracking
/// - **Conversation Continuity**: PreviousResponseId for maintaining context
/// - **Detailed Token Metrics**: Tracks input, output, reasoning, and cached tokens
/// - **Response Items**: Structured output with reasoning and message items
///
/// These tests verify:
/// - Schema compatibility with the Response API
/// - Polymorphic deserialization of responses
/// - Token usage tracking and metrics
/// - Reasoning capabilities integration
/// </summary>
[TestFixture]
public class ResponseApiTests : SemanticKernelTestBase
{
    /// <summary>
    /// **Real LLM test using the new OpenAI Response API directly (without Semantic Kernel)**
    ///
    /// This test validates NextStep schema generation using the new Azure OpenAI Response API.
    /// The Response API provides:
    /// - Direct access to OpenAI models without SK abstractions
    /// - Extended reasoning capabilities with reasoning tokens tracking
    /// - Conversation continuity via PreviousResponseId
    /// - Detailed token usage statistics including cached and reasoning tokens
    ///
    /// This test verifies:
    /// - Schema compatibility with the Response API
    /// - Polymorphic deserialization of the response
    /// - Token usage tracking (input, output, reasoning, cached)
    ///
    /// **NOTE**: This test is currently commented out because the Response API types
    /// (ResponseItem, ResponseCreationOptions, etc.) are not yet available in the
    /// OpenAI SDK version 2.5.0. These types are expected in a future release.
    /// Uncomment and update the namespace when the API becomes available.
    /// </summary>
    [Test]
    [Category("LLMIntegration")]
    public async Task GenerateNextStepSchema_WithResponseAPI_PolymorphicDeserialization()
    {

        // **Arrange**: Skip test if no API key is available
        var azureApiKey = Dotenv.Get("OPENAI_API_KEY");
        var azureEndpoint = Dotenv.Get("AZURE_ENDPOINT");

        if (string.IsNullOrEmpty(azureApiKey) || string.IsNullOrEmpty(azureEndpoint))
        {
            Assert.Ignore("OPENAI_API_KEY or AZURE_ENDPOINT environment variable not set");
            return;
        }

        try
        {
            // **Arrange**: Generate the JSON schema for NextStep with polymorphic ToolCall support
            var schemaJson = GeneratePolymorphicNextStepJsonSchema();
            Console.WriteLine("Generated JSON Schema:");
            Console.WriteLine(schemaJson);

            // **Arrange**: Configure Azure OpenAI client with the latest API version
            // Using AzureOpenAIClientOptions.ServiceVersion.V2025_04_01_Preview for Response API support
            var clientOptions = new AzureOpenAIClientOptions(
                AzureOpenAIClientOptions.ServiceVersion.V2025_04_01_Preview);

            var client = new AzureOpenAIClient(
                new Uri(azureEndpoint),
                new ApiKeyCredential(azureApiKey),
                clientOptions);

            // **Arrange**: Get the Response client for the specified deployment
            var responseClient = client.GetOpenAIResponseClient("gpt-5-nano");

            // **Arrange**: Create the user prompt requesting workflow step data
            var userPrompt = @"Please format this workflow step data into JSON:
    Current workflow state: 'Ready to send notification email'
    Remaining steps: ['Send email to customer', 'Wait for confirmation', 'Update status']
    Task is not yet completed.
    Next action: Send an email with subject 'Order Confirmation' and message 'Your order #12345 has been processed successfully' to customer@example.com with attached receipt.pdf

    You must respond with valid JSON matching the provided schema.";

            var inputItems = new List<ResponseItem>
            {
                ResponseItem.CreateUserMessageItem(userPrompt)
            };

            // **Arrange**: Configure response creation options with reasoning capabilities
            var options = new ResponseCreationOptions
            {
                // No previous conversation - this is a single-shot request
                PreviousResponseId = null,
                // Enable reasoning with low effort level for faster responses
                ReasoningOptions = new ResponseReasoningOptions
                {
                    ReasoningEffortLevel = ResponseReasoningEffortLevel.Low
                },

                TextOptions = new ResponseTextOptions
                {
                    TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                        jsonSchemaFormatName: "NextStep",
                        jsonSchema: BinaryData.FromString(schemaJson),
                        jsonSchemaFormatDescription: "Schema for NextStep with polymorphic ToolCall support",
                        jsonSchemaIsStrict: true)
                },
            };

            Console.WriteLine("\n📡 Calling OpenAI Response API...");

            // **Act**: Make the LLM call using the Response API
            var result = await responseClient.CreateResponseAsync(inputItems, options);
            OpenAIResponse response = result;

            Console.WriteLine($"✅ Response received - ID: {response.Id}");
            Console.WriteLine($"   Model: {response.Model}");

            // **Assert**: Extract and display reasoning process (if available)
            string? assistantMessage = null;
            foreach (var outputItem in response.OutputItems)
            {
                if (outputItem is ReasoningResponseItem reasoning)
                {
                    var summaryText = reasoning.GetSummaryText();
                    var reasoningContent = !string.IsNullOrWhiteSpace(summaryText)
                        ? summaryText
                        : $"Reasoning ID: {reasoning.Id}";

                    Console.WriteLine("\n🧠 Reasoning Process:");
                    Console.WriteLine($"   {reasoningContent}");
                }
                else if (outputItem is MessageResponseItem message)
                {
                    // Extract the assistant's response
                    assistantMessage = message.Content[0].Text;
                    Console.WriteLine("\n💬 Assistant Response:");
                    Console.WriteLine(assistantMessage);
                }
            }

            // **Assert**: Verify we got a response
            Assert.That(assistantMessage, Is.Not.Null.And.Not.Empty, "Assistant should return a response");

            // **Assert**: Display token usage statistics
            var usage = response.Usage;
            Console.WriteLine("\n📊 Token Usage Statistics:");
            Console.WriteLine($"   Input Tokens:  {usage.InputTokenCount}");
            Console.WriteLine($"   Output Tokens: {usage.OutputTokenCount}");
            Console.WriteLine($"   Total Tokens:  {usage.TotalTokenCount}");

            // Display input token details (cached tokens)
            if (usage.InputTokenDetails?.CachedTokenCount > 0)
            {
                Console.WriteLine($"   └─ Cached Input: {usage.InputTokenDetails.CachedTokenCount}");
            }

            // Display output token details (reasoning tokens)
            if (usage.OutputTokenDetails?.ReasoningTokenCount > 0)
            {
                Console.WriteLine($"   └─ Reasoning Output: {usage.OutputTokenDetails.ReasoningTokenCount}");
            }

            // **Assert**: Deserialize the JSON response using NextStepManager
            var manager = new NextStepManager()
                .AddDerivedType<SendEmailToolCall>()
                .AddDerivedType<GetCustomerDataToolCall>()
                .AddDerivedType<IssueInvoiceToolCall>();

            var nextStep = manager.DeserializeFromJson(assistantMessage!);

            // **Assert**: Verify the deserialized NextStep object
            Assert.That(nextStep, Is.Not.Null, "Should deserialize to NextStep object");
            Assert.That(nextStep.NextStepToolToCall, Is.Not.Null, "ToolCall should not be null");
            Assert.That(nextStep.CurrentState, Is.Not.Null.And.Not.Empty, "CurrentState should be populated");
            Assert.That(nextStep.PlanRemainingStepsBrief, Is.Not.Null.And.Not.Empty, "PlanRemainingStepsBrief should be populated");
            Assert.That(nextStep.TaskCompleted, Is.False, "TaskCompleted should be false as requested");

            // **Assert**: Verify polymorphic deserialization - should be a SendEmailToolCall
            Assert.That(nextStep.NextStepToolToCall, Is.TypeOf<SendEmailToolCall>(),
                "ToolCall should deserialize as SendEmailToolCall type");

            var sendEmailCall = nextStep.NextStepToolToCall as SendEmailToolCall;
            Assert.That(sendEmailCall, Is.Not.Null, "Should be able to cast ToolCall to SendEmailToolCall");
            Assert.That(sendEmailCall!.Subject, Is.Not.Null.And.Not.Empty, "Email subject should not be empty");
            Assert.That(sendEmailCall.Message, Is.Not.Null.And.Not.Empty, "Email message should not be empty");
            Assert.That(sendEmailCall.RecipientEmail, Is.Not.Null.And.Not.Empty, "Recipient email should not be empty");

            Assert.That(sendEmailCall.Subject.ToLower(), Contains.Substring("confirmation").Or.Contains("order"),
                "Should extract order confirmation subject");
            Assert.That(sendEmailCall.RecipientEmail.ToLower(), Contains.Substring("customer@example.com"),
                "Should extract recipient email");

            Console.WriteLine($"\n✅ Successfully extracted NextStep: '{nextStep.CurrentState}' with SendEmailToolCall");
            Console.WriteLine($"✅ Email details: '{sendEmailCall.Subject}' to '{sendEmailCall.RecipientEmail}'");
            Console.WriteLine("✅ Polymorphic deserialization successful using Response API!");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Failed to perform Response API call: {ex.Message}\nStack trace: {ex.StackTrace}");
        }

        Console.WriteLine("\n🎉 Real LLM call with Response API and polymorphic NextStep schema validation completed successfully!");

        await Task.CompletedTask; // Placeholder to avoid async warning
    }

    /// <summary>
    /// **Helper method to generate polymorphic JSON schema for NextStep**
    ///
    /// Creates a NextStepManager configured with the standard set of ToolCall types
    /// and generates the JSON schema for use in Response API calls.
    ///
    /// The schema includes:
    /// - SendEmailToolCall: For email operations
    /// - GetCustomerDataToolCall: For customer data retrieval
    /// - IssueInvoiceToolCall: For invoice creation
    /// </summary>
    /// <returns>JSON schema string with proper polymorphic ToolCall support</returns>
    private static string GeneratePolymorphicNextStepJsonSchema()
    {
        var manager = new NextStepManager()
            .AddDerivedType<SendEmailToolCall>()
            .AddDerivedType<GetCustomerDataToolCall>()
            .AddDerivedType<IssueInvoiceToolCall>();

        return manager.GenerateSchema();
    }
}
