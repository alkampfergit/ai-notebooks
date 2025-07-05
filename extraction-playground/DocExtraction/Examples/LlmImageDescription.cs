#pragma warning disable OPENAI001

using Azure;
using Azure.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using Spectre.Console;
using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;
using SkiaSharp;

namespace DocExtraction.Examples;

/// <summary>
/// Schema for structured JSON output with short and long descriptions
/// </summary>
public class ImageDescriptionSchema
{
    [JsonPropertyName("short_description")]
    public string ShortDescription { get; set; } = string.Empty;

    [JsonPropertyName("long_description")]
    public string LongDescription { get; set; } = string.Empty;
}

public static class LlmImageDescription
{
    private const string DefaultPdfPath = "/Users/gianmariaricci/Downloads/animals.pdf";

    public static async Task RunAsync()
    {
        // Ask user for PDF file path with default value
        var filePath = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter the [green]PDF file path[/]:")
                .DefaultValue(DefaultPdfPath)
                .ShowDefaultValue());

        // Validate the file exists
        if (!File.Exists(filePath))
        {
            AnsiConsole.MarkupLine("[red]Error: File not found![/]");
            return;
        }

        // Validate it's a PDF file
        if (!Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[red]Error: File is not a PDF![/]");
            return;
        }

        // Setup directories
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var outputDirectory = Path.Combine(baseDirectory, "llm-image-description-output");
        var rendersDirectory = Path.Combine(outputDirectory, "renders");

        // Check if images already exist
        bool shouldReExtract = true;
        if (Directory.Exists(rendersDirectory) && Directory.GetFiles(rendersDirectory, "*.jpg").Length > 0)
        {
            shouldReExtract = AnsiConsole.Confirm(
                "[yellow]Images already exist. Do you want to re-extract images from the PDF?[/]",
                defaultValue: false);
        }

        // Clear and recreate directory if re-extracting
        if (shouldReExtract)
        {
            if (Directory.Exists(outputDirectory))
            {
                AnsiConsole.MarkupLine("[yellow]Clearing existing output directory...[/]");
                Directory.Delete(outputDirectory, true);
            }
            Directory.CreateDirectory(rendersDirectory);

            AnsiConsole.MarkupLine($"[blue]Output directory:[/] {outputDirectory}");
            AnsiConsole.MarkupLine($"[blue]Renders directory:[/] {rendersDirectory}");
            AnsiConsole.WriteLine();

            // Extract images from PDF
            await ExtractImagesFromPdfAsync(filePath, rendersDirectory);
        }
        else
        {
            AnsiConsole.MarkupLine("[cyan]Using existing images...[/]");
            AnsiConsole.WriteLine();
        }

        // Ask if user wants to generate text descriptions
        var generateTextDescriptions = AnsiConsole.Confirm(
            "[yellow]Do you want to generate text descriptions for the images?[/]",
            defaultValue: true);

        // Ask if user wants to generate JSON descriptions
        var generateJsonDescriptions = AnsiConsole.Confirm(
            "[yellow]Do you want to generate JSON descriptions (short and long)?[/]",
            defaultValue: false);

        // Generate descriptions if requested
        if (generateTextDescriptions || generateJsonDescriptions)
        {
            await GenerateDescriptionsAsync(rendersDirectory, generateTextDescriptions, generateJsonDescriptions);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Skipping description generation...[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold green]Completed[/]"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold green]✓[/] All images processed");
        AnsiConsole.MarkupLine($"  [blue]Output directory:[/] {rendersDirectory}");
        AnsiConsole.WriteLine();
    }

    private static Task ExtractImagesFromPdfAsync(string filePath, string rendersDirectory)
    {
        try
        {
            using var document = PdfDocument.Open(filePath);
            document.AddSkiaPageFactory();
            var pageCount = document.NumberOfPages;

            AnsiConsole.MarkupLine($"[green]Extracting {pageCount} pages as images...[/]");
            AnsiConsole.WriteLine();

            const int maxHorizontalResolution = 1024;

            AnsiConsole.Progress()
                .Start(ctx =>
                {
                    var task = ctx.AddTask("[green]Extracting PDF pages[/]", maxValue: pageCount);

                    for (int pageNumber = 1; pageNumber <= pageCount; pageNumber++)
                    {
                        var outputFileName = Path.Combine(rendersDirectory, $"page_{pageNumber:D3}.jpg");

                        // Get page to calculate aspect ratio
                        var page = document.GetPage(pageNumber);
                        var pageWidth = page.Width;

                        // Calculate scale factor to limit horizontal resolution to 1024
                        var scale = (float)Math.Min(1.0, maxHorizontalResolution / pageWidth);
                        var dpi = (int)(72 * scale); // 72 is the base DPI for PDF

                        // Render page to PNG stream first, then convert to JPEG with quality control
                        using (var pngStream = document.GetPageAsPng(pageNumber, scale, dpi))
                        {
                            pngStream.Position = 0; // Reset stream position for decoding
                            using (var skBitmap = SKBitmap.Decode(pngStream))
                            using (var image = SKImage.FromBitmap(skBitmap))
                            using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 80))
                            using (var fs = new FileStream(outputFileName, FileMode.Create))
                            {
                                data.SaveTo(fs);
                            }
                        }

                        task.Increment(1);
                    }
                });

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold green]✓[/] Successfully extracted {pageCount} pages");
            AnsiConsole.WriteLine();

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error extracting images:[/] {ex.Message}");
            AnsiConsole.WriteException(ex);
            throw;
        }
    }

    private static async Task GenerateDescriptionsAsync(string rendersDirectory, bool generateTextDescriptions, bool generateJsonDescriptions)
    {
        // Get all JPG files
        var imageFiles = Directory.GetFiles(rendersDirectory, "page_*.jpg")
            .OrderBy(f => f)
            .ToList();

        if (imageFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No images found in the renders directory![/]");
            return;
        }

        // Load Azure OpenAI credentials
        var azureOpenAIEndpoint = Dotenv.Get("AZURE_ENDPOINT");
        var azureOpenAIKey = Dotenv.Get("OPENAI_API_KEY");
        var azureOpenAIDeployment = "gpt-5-nano";

        if (string.IsNullOrEmpty(azureOpenAIEndpoint) || string.IsNullOrEmpty(azureOpenAIKey))
        {
            AnsiConsole.MarkupLine("[red]Error: AZURE_ENDPOINT and OPENAI_API_KEY must be set in .env file![/]");
            AnsiConsole.MarkupLine("[yellow]Cannot generate descriptions without Azure OpenAI credentials.[/]");
            return;
        }

        // Initialize Azure OpenAI Client
        var clientOptions = new AzureOpenAIClientOptions(
            AzureOpenAIClientOptions.ServiceVersion.V2025_04_01_Preview);

        var client = new AzureOpenAIClient(
            new Uri(azureOpenAIEndpoint),
            new ApiKeyCredential(azureOpenAIKey),
            clientOptions);

        var responseClient = client.GetOpenAIResponseClient(azureOpenAIDeployment);
        var chatClient = client.GetChatClient(azureOpenAIDeployment);

        AnsiConsole.MarkupLine("[green]Azure OpenAI client initialized[/]");
        AnsiConsole.MarkupLine($"[blue]Using deployment:[/] {azureOpenAIDeployment}");
        AnsiConsole.WriteLine();

        // Process each image
        var totalTasks = (generateTextDescriptions ? imageFiles.Count : 0) + (generateJsonDescriptions ? imageFiles.Count : 0);
        AnsiConsole.MarkupLine($"[yellow]Processing {imageFiles.Count} images with {azureOpenAIDeployment}...[/]");
        if (generateTextDescriptions) AnsiConsole.MarkupLine("[yellow]  → Generating text descriptions (Response API)[/]");
        if (generateJsonDescriptions) AnsiConsole.MarkupLine("[yellow]  → Generating JSON descriptions with structured output (Chat API)[/]");
        AnsiConsole.WriteLine();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Generating descriptions[/]", maxValue: totalTasks);

                for (int i = 0; i < imageFiles.Count; i++)
                {
                    var imagePath = imageFiles[i];
                    var imageFileName = Path.GetFileNameWithoutExtension(imagePath);

                    AnsiConsole.MarkupLine($"  [cyan]→[/] Processing {imageFileName}.jpg ({i + 1}/{imageFiles.Count})");

                    // Generate text description if requested
                    if (generateTextDescriptions)
                    {
                        await GenerateTextDescriptionAsync(responseClient, imagePath, imageFileName, rendersDirectory);
                        task.Increment(1);
                    }

                    // Generate JSON description if requested
                    if (generateJsonDescriptions)
                    {
                        await GenerateJsonDescriptionAsync(responseClient, imagePath, imageFileName, rendersDirectory);
                        task.Increment(1);
                    }
                }
            });
    }

    private static async Task GenerateTextDescriptionAsync(OpenAIResponseClient responseClient, string imagePath, string imageFileName, string rendersDirectory)
    {
        var descriptionFilePath = Path.Combine(rendersDirectory, $"{imageFileName}.txt");

        try
        {
            // Delete existing description file if it exists
            if (File.Exists(descriptionFilePath))
            {
                File.Delete(descriptionFilePath);
            }

            // Read the image file
            using var stream = File.OpenRead(imagePath);
            var binaryData = await BinaryData.FromStreamAsync(stream);

            var image = ResponseContentPart.CreateInputImagePart(
                imageBytes: binaryData,
                imageBytesMediaType: "image/png",
                imageDetailLevel: ResponseImageDetailLevel.High);

            ResponseItem request = ResponseItem.CreateUserMessageItem(
                [
                    ResponseContentPart.CreateInputTextPart("Please provide a detailed description of this image. Include all visible text, diagrams, charts, tables, and other elements you can see. Be thorough and descriptive."),
                    image
                ]
            );
            var inputItems = new List<ResponseItem>
            {
                request
            };

            // Configure response options
            var options = new ResponseCreationOptions();
            options.ReasoningOptions = new ResponseReasoningOptions
            {
                ReasoningEffortLevel = ResponseReasoningEffortLevel.High
            };

            // Call the Response Model API
            var result = await responseClient.CreateResponseAsync(inputItems, options);
            OpenAIResponse response = result;

            // Extract description from response
            var assistantMessage = response.OutputItems.OfType<MessageResponseItem>().FirstOrDefault();
            string imageDescription;
            if (assistantMessage == null)
            {
                imageDescription = "No description generated.";
            }
            else
            {
                imageDescription = assistantMessage.Content.FirstOrDefault()?.Text ?? "No description generated.";
            }

            // Save description to text file
            await File.WriteAllTextAsync(descriptionFilePath, imageDescription);

            AnsiConsole.MarkupLine($"    [green]✓[/] Text description saved ({imageDescription.Length} characters)");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"    [red]✗[/] Text description failed: {ex.Message}");

            // Write error to description file
            await File.WriteAllTextAsync(descriptionFilePath, $"[Error: {ex.Message}]");
        }
    }

    private static async Task GenerateJsonDescriptionAsync(OpenAIResponseClient responseClient, string imagePath, string imageFileName, string rendersDirectory)
    {
        var jsonFilePath = Path.Combine(rendersDirectory, $"{imageFileName}.json");

        try
        {
            // Delete existing JSON file if it exists
            if (File.Exists(jsonFilePath))
            {
                File.Delete(jsonFilePath);
            }

            // Read the image file
            using var stream = File.OpenRead(imagePath);
            var binaryData = await BinaryData.FromStreamAsync(stream);

            var image = ResponseContentPart.CreateInputImagePart(
                imageBytes: binaryData,
                imageBytesMediaType: "image/png",
                imageDetailLevel: ResponseImageDetailLevel.High);

            ResponseItem request = ResponseItem.CreateUserMessageItem(
                [
                    ResponseContentPart.CreateInputTextPart(@"""Please analyze the image and provide the following information in JSON format
- short: A brief one-sentence summary of the image (max 100 characters)
- long: A detailed, comprehensive description of the image including all visible elements
- keywords: a list of relevant keywords you can associate with the image
- categories: a list of relevant categories you can associate with the image
                    """),
                    image
                ]
            );
            var inputItems = new List<ResponseItem>
            {
                request
            };

            // Configure response options with structured output
            var options = new ResponseCreationOptions();
            options.ReasoningOptions = new ResponseReasoningOptions
            {
                ReasoningEffortLevel = ResponseReasoningEffortLevel.High
            };

            // Define JSON schema for structured output
            var jsonSchema = BinaryData.FromString("""
            {
              "type": "object",
              "properties": {
                "short": {
                  "type": "string"
                },
                "long": {
                  "type": "string"
                },
                "keywords": {
                  "type": "array",
                  "items": {
                    "type": "string"
                  }
                },
                "categories": {
                  "type": "array",
                  "items": {
                    "type": "string"
                  }
                }
              },
              "required": ["short", "long", "keywords", "categories"],
              "additionalProperties": false
            }
            """);

            options.TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "ImageDescriptionSchema",
                    jsonSchema: jsonSchema,
                    jsonSchemaIsStrict: true)
            };

            // Call the Response Model API
            var result = await responseClient.CreateResponseAsync(inputItems, options);
            OpenAIResponse response = result;

            // Extract JSON response
            var assistantMessage = response.OutputItems.OfType<MessageResponseItem>().FirstOrDefault();
            string jsonResponse;
            if (assistantMessage == null)
            {
                jsonResponse = @"{""short_description"": ""No description generated."", ""long_description"": ""No description generated.""}";
            }
            else
            {
                jsonResponse = assistantMessage.Content.FirstOrDefault()?.Text ?? @"{""short_description"": ""No description generated."", ""long_description"": ""No description generated.""}";
            }

            // Save JSON to file
            await File.WriteAllTextAsync(jsonFilePath, jsonResponse);

            AnsiConsole.MarkupLine($"    [green]✓[/] JSON description saved {jsonResponse.Length} chars)");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"    [red]✗[/] JSON description failed: {ex.Message}");

            // Write error to JSON file
            var errorJson = JsonSerializer.Serialize(new ImageDescriptionSchema
            {
                ShortDescription = $"[Error: {ex.Message}]",
                LongDescription = $"[Error: {ex.Message}]"
            }, new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(jsonFilePath, errorJson);
        }
    }
}

#pragma warning restore OPENAI001
