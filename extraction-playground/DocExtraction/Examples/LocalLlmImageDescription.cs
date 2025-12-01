#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Spectre.Console;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;
using SkiaSharp;

namespace DocExtraction.Examples;

/// <summary>
/// Example that uses a local LLM (Gemma) via Semantic Kernel to describe PDF page images.
/// Requires a local LLM server running on localhost:1234 (e.g., LM Studio, Ollama with OpenAI-compatible API).
/// </summary>
public static class LocalLlmImageDescription
{
    private const string DefaultPdfPath = "C:\\temp\\manualedreame2.pdf";
    private const string LocalLlmEndpoint = "http://localhost:1234/v1";
    private const string ModelName = "gemma-3-4b";

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

        // Ask for LLM endpoint
        var llmEndpoint = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter the [green]Local LLM endpoint[/]:")
                .DefaultValue(LocalLlmEndpoint)
                .ShowDefaultValue());

        // Ask for model name
        var modelName = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter the [green]Model name[/]:")
                .DefaultValue(ModelName)
                .ShowDefaultValue());

        // Setup directories
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var outputDirectory = Path.Combine(baseDirectory, "local-llm-description-output");
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
            ExtractImagesFromPdf(filePath, rendersDirectory);
        }
        else
        {
            AnsiConsole.MarkupLine("[cyan]Using existing images...[/]");
            AnsiConsole.WriteLine();
        }

        // Get total page count
        var imageFiles = Directory.GetFiles(rendersDirectory, "page_*.jpg")
            .OrderBy(f => f)
            .ToList();

        if (imageFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: No images found in the renders directory![/]");
            return;
        }

        // Initialize Semantic Kernel
        var (kernel, chatService) = InitializeSemanticKernel(llmEndpoint, modelName);

        AnsiConsole.MarkupLine("[green]Semantic Kernel initialized with local LLM[/]");
        AnsiConsole.WriteLine();

        // Start interactive loop
        await RunInteractiveLoopAsync(chatService, rendersDirectory, imageFiles.Count);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold green]Completed[/]"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold green]✓[/] Session ended");
        AnsiConsole.MarkupLine($"  [blue]Output directory:[/] {rendersDirectory}");
        AnsiConsole.WriteLine();
    }

    private static (Kernel kernel, IChatCompletionService chatService) InitializeSemanticKernel(string llmEndpoint, string modelName)
    {
        AnsiConsole.MarkupLine($"[cyan]Connecting to local LLM at:[/] {llmEndpoint}");
        AnsiConsole.MarkupLine($"[cyan]Using model:[/] {modelName}");
        AnsiConsole.WriteLine();

        var builder = Kernel.CreateBuilder();

        builder.AddOpenAIChatCompletion(
            modelId: modelName,
            endpoint: new Uri(llmEndpoint),
            apiKey: "not-needed"
        );

        var kernel = builder.Build();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        return (kernel, chatService);
    }

    private static async Task RunInteractiveLoopAsync(IChatCompletionService chatService, string rendersDirectory, int totalPages)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Interactive Page Description[/]"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Total pages available: {totalPages}[/]");
        AnsiConsole.MarkupLine("[dim]Enter page numbers to describe, 'all' to process all pages, or 'exit' to quit.[/]");
        AnsiConsole.MarkupLine("[dim]You can enter multiple pages separated by commas (e.g., 1,3,5) or ranges (e.g., 1-5).[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>($"Enter [green]page number(s)[/] (1-{totalPages}, 'all', or 'exit'):")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(input) ||
                input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[yellow]Exiting interactive mode...[/]");
                break;
            }

            // Parse page numbers
            var pageNumbers = ParsePageNumbers(input, totalPages);

            if (pageNumbers.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No valid page numbers entered. Please try again.[/]");
                AnsiConsole.WriteLine();
                continue;
            }

            AnsiConsole.MarkupLine($"[cyan]Processing {pageNumbers.Count} page(s): {string.Join(", ", pageNumbers)}[/]");
            AnsiConsole.WriteLine();

            // Process selected pages
            foreach (var pageNumber in pageNumbers)
            {
                var imagePath = Path.Combine(rendersDirectory, $"page_{pageNumber:D3}.jpg");
                var imageFileName = $"page_{pageNumber:D3}";

                if (!File.Exists(imagePath))
                {
                    AnsiConsole.MarkupLine($"  [red]✗[/] Page {pageNumber}: Image file not found");
                    continue;
                }

                AnsiConsole.MarkupLine($"  [cyan]→[/] Processing page {pageNumber}...");

                await GenerateDescriptionAsync(chatService, imagePath, imageFileName, rendersDirectory);
            }

            AnsiConsole.WriteLine();
        }
    }

    private static List<int> ParsePageNumbers(string input, int totalPages)
    {
        var pageNumbers = new HashSet<int>();

        // Handle "all" keyword
        if (input.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return Enumerable.Range(1, totalPages).ToList();
        }

        // Split by comma and process each part
        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            // Check if it's a range (e.g., "1-5")
            if (part.Contains('-'))
            {
                var rangeParts = part.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (rangeParts.Length == 2 &&
                    int.TryParse(rangeParts[0], out int start) &&
                    int.TryParse(rangeParts[1], out int end))
                {
                    // Clamp to valid range
                    start = Math.Max(1, Math.Min(start, totalPages));
                    end = Math.Max(1, Math.Min(end, totalPages));

                    // Add all pages in range
                    for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                    {
                        pageNumbers.Add(i);
                    }
                }
            }
            else if (int.TryParse(part, out int pageNumber))
            {
                // Single page number
                if (pageNumber >= 1 && pageNumber <= totalPages)
                {
                    pageNumbers.Add(pageNumber);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning: Page {pageNumber} is out of range (1-{totalPages})[/]");
                }
            }
        }

        return pageNumbers.OrderBy(p => p).ToList();
    }

    private static void ExtractImagesFromPdf(string filePath, string rendersDirectory)
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
                            pngStream.Position = 0;
                            using var skBitmap = SKBitmap.Decode(pngStream);
                            using var image = SKImage.FromBitmap(skBitmap);
                            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
                            using var fs = new FileStream(outputFileName, FileMode.Create);
                            data.SaveTo(fs);
                        }

                        task.Increment(1);
                    }
                });

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold green]✓[/] Successfully extracted {pageCount} pages");
            AnsiConsole.WriteLine();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error extracting images:[/] {ex.Message}");
            AnsiConsole.WriteException(ex);
            throw;
        }
    }

    private static async Task GenerateDescriptionAsync(
        IChatCompletionService chatService,
        string imagePath,
        string imageFileName,
        string rendersDirectory)
    {
        var descriptionFilePath = Path.Combine(rendersDirectory, $"{imageFileName}.txt");

        try
        {
            // Delete existing description file if it exists
            if (File.Exists(descriptionFilePath))
            {
                File.Delete(descriptionFilePath);
            }

            // Read the image file and convert to base64
            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var base64Image = Convert.ToBase64String(imageBytes);

            // Create chat history with image
            var chatHistory = new ChatHistory();

            // Add system message
            chatHistory.AddSystemMessage(
                "You are a helpful assistant that describes images in detail. " +
                "Provide comprehensive descriptions including all visible text, diagrams, charts, tables, and other elements.");

            // Add user message with image
            var imageContent = new ImageContent(imageBytes, "image/jpeg");
            var textContent = new TextContent(
                "Please provide a detailed description of this image. " +
                "Include all visible text, diagrams, charts, tables, and other elements you can see. " +
                "Be thorough and descriptive.");

            var userMessage = new ChatMessageContent(AuthorRole.User, [textContent, imageContent]);
            chatHistory.Add(userMessage);

            // Get response from local LLM
            var response = await chatService.GetChatMessageContentAsync(chatHistory);

            var imageDescription = response.Content ?? "No description generated.";

            // Save description to text file
            await File.WriteAllTextAsync(descriptionFilePath, imageDescription);

            AnsiConsole.MarkupLine($"    [green]✓[/] Description saved ({imageDescription.Length} characters)");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"    [red]✗[/] Failed: {ex.Message}");

            // Write error to description file
            await File.WriteAllTextAsync(descriptionFilePath, $"[Error: {ex.Message}]");
        }
    }
}

#pragma warning restore SKEXP0010
#pragma warning restore SKEXP0070
