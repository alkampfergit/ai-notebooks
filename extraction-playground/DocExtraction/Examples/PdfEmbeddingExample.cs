using Azure;
using Azure.AI.Inference;
using Azure.Core;
using Azure.Core.Pipeline;
using Spectre.Console;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;

namespace DocExtraction.Examples;

/// <summary>
/// Custom HTTP policy to add the extra-parameters header to handle Azure embedding API parameter validation.
/// 
/// This addresses the "Extra Parameters ['stream'] Not Allowed" error that emerged in Azure's embedding API
/// (January 2026). The API now rejects unknown parameters by default. This policy adds the "extra-parameters" 
/// header with value "drop" to instruct the API to silently remove unknown parameters instead of throwing errors.
/// 
/// Valid values for parameter handling:
/// - "error" (default): Returns 400 error if extra parameters exist
/// - "drop": Silently removes unknown parameters (recommended)
/// - "pass-through": Forwards unknown parameters to the underlying model
/// </summary>
internal class ExtraParametersPolicy : HttpPipelinePolicy
{
    private readonly string _parameterHandling;

    public ExtraParametersPolicy(string parameterHandling = "drop")
    {
        _parameterHandling = parameterHandling;
    }

    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        message.Request.Headers.Add("extra-parameters", _parameterHandling);
        ProcessNext(message, pipeline);
    }

    public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        message.Request.Headers.Add("extra-parameters", _parameterHandling);
        return ProcessNextAsync(message, pipeline);
    }
}

public static class PdfEmbeddingExample
{
    private const string DefaultPdfPath = "S:\\OneDrive\\develop\\CorsiPresentazioni\\20251202 - WPC\\animals.pdf";
    private const string VectorDbFileName = "vectordb.json";

    public static async Task RunAsync()
    {
        // Setup directories
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var outputDirectory = Path.Combine(baseDirectory, "pdf-embedding-output");
        var rendersDirectory = Path.Combine(outputDirectory, "renders");
        var vectorDbPath = Path.Combine(outputDirectory, VectorDbFileName);

        // Ask user what they want to do
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
                .AddChoices(
                [
                    "Process a new PDF",
                    "Load existing vector database and search"
                ]));

        SimpleVectorStore vectorStore;
        string? azureInferenceEndpoint;
        string? azureInferenceCredential;

        if (choice == "Process a new PDF")
        {
            vectorStore = await ProcessNewPdfAsync(outputDirectory, rendersDirectory, vectorDbPath);

            // Get credentials for search
            azureInferenceEndpoint = Dotenv.Get("AZURE_EMBEDDING_DEPLOYMENT");
            azureInferenceCredential = Dotenv.Get("AZURE_EMBEDDING_KEY");
        }
        else
        {
            // Load existing vector database
            if (!File.Exists(vectorDbPath))
            {
                AnsiConsole.MarkupLine($"[red]Error: Vector database not found at {vectorDbPath}[/]");
                AnsiConsole.MarkupLine("[yellow]Please process a PDF first to create a vector database.[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[cyan]Loading vector database from:[/] {vectorDbPath}");
            try
            {
                vectorStore = SimpleVectorStore.LoadFromFile(vectorDbPath);
                AnsiConsole.MarkupLine($"[green]✓ Loaded {vectorStore.Count} vectors from database[/]");
                AnsiConsole.WriteLine();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error loading vector database:[/] {ex.Message}");
                return;
            }

            // Get credentials for search
            azureInferenceEndpoint = Dotenv.Get("AZURE_EMBEDDING_DEPLOYMENT");
            azureInferenceCredential = Dotenv.Get("AZURE_EMBEDDING_KEY");

            if (string.IsNullOrEmpty(azureInferenceEndpoint) || string.IsNullOrEmpty(azureInferenceCredential))
            {
                AnsiConsole.MarkupLine("[yellow]Warning: AZURE_EMBEDDING_DEPLOYMENT and AZURE_EMBEDDING_KEY not set in environment[/]");
                AnsiConsole.MarkupLine("[yellow]Cannot perform searches without these credentials.[/]");
                return;
            }
        }

        // Perform text-based similarity search
        await PerformSearchLoopAsync(vectorStore, azureInferenceEndpoint!, azureInferenceCredential!);
    }

    private static async Task<SimpleVectorStore> ProcessNewPdfAsync(string outputDirectory, string rendersDirectory, string vectorDbPath)
    {
        // Ask user for PDF file path with default value
        var filePath = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter the [green]PDF file path[/]:")
                .DefaultValue(DefaultPdfPath)
                .ShowDefaultValue());

        // Validate the file exists
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("PDF file not found", filePath);
        }

        // Validate it's a PDF file
        if (!Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("File is not a PDF");
        }

        // Clear and recreate output directory
        if (Directory.Exists(outputDirectory))
        {
            AnsiConsole.MarkupLine("[yellow]Clearing existing output directory...[/]");
            Directory.Delete(outputDirectory, true);
        }
        Directory.CreateDirectory(rendersDirectory);

        AnsiConsole.MarkupLine($"[blue]Output directory:[/] {outputDirectory}");
        AnsiConsole.MarkupLine($"[blue]Renders directory:[/] {rendersDirectory}");
        AnsiConsole.WriteLine();

        try
        {
            // Step 1: Render all PDF pages to images
            int pageCount;
            using (var document = PdfDocument.Open(filePath))
            {
                document.AddSkiaPageFactory();
                pageCount = document.NumberOfPages;

                AnsiConsole.MarkupLine($"[green]Rendering {pageCount} pages...[/]");
                AnsiConsole.WriteLine();

                // Render all pages with progress bar
                const int maxHorizontalResolution = 1024;

                AnsiConsole.Progress()
                    .Start(ctx =>
                    {
                        var task = ctx.AddTask("[green]Rendering PDF pages[/]", maxValue: pageCount);

                        for (int pageNumber = 1; pageNumber <= 4; pageNumber++)
                        {
                            var outputFileName = Path.Combine(rendersDirectory, $"page_{pageNumber:D3}.png");

                            // Get page to calculate aspect ratio
                            var page = document.GetPage(pageNumber);
                            var pageWidth = page.Width;
                            var pageHeight = page.Height;

                            // Calculate scale factor to limit horizontal resolution to 1024
                            var scale = (float)Math.Min(1.0, maxHorizontalResolution / pageWidth);
                            var dpi = (int)(72 * scale); // 72 is the base DPI for PDF

                            using (var fs = new FileStream(outputFileName, FileMode.Create))
                            using (var ms = document.GetPageAsPng(pageNumber, scale, dpi))
                            {
                                ms.WriteTo(fs);
                            }

                            task.Increment(1);
                        }
                    });

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[bold green]✓[/] Successfully rendered {pageCount} pages");
                AnsiConsole.WriteLine();
            }

            // Step 2: Initialize Azure Embedding Client
            var azureInferenceEndpoint = Dotenv.Get("AZURE_EMBEDDING_ENDPOINT");
            var azureInferenceCredential = Dotenv.Get("AZURE_EMBEDDING_KEY");
            var azureInferenceModel = Dotenv.Get("AZURE_EMBEDDING_MODEL") ?? "Cohere-embed-v3-multilingual";

            if (string.IsNullOrEmpty(azureInferenceEndpoint) || string.IsNullOrEmpty(azureInferenceCredential))
            {
                AnsiConsole.MarkupLine("[yellow]Warning: AZURE_EMBEDDING_ENDPOINT and AZURE_EMBEDDING_KEY not set in environment[/]");
                AnsiConsole.MarkupLine("[yellow]Skipping embedding generation (set these variables to enable)[/]");
                AnsiConsole.WriteLine();

                AnsiConsole.MarkupLine("[blue]All pages have been rendered to:[/]");
                AnsiConsole.MarkupLine($"  [green]{rendersDirectory}[/]");
                throw new InvalidOperationException("Azure credentials not configured");
            }

            var clientOptions = new AzureAIInferenceClientOptions();
            clientOptions.AddPolicy(new ExtraParametersPolicy("drop"), HttpPipelinePosition.PerCall);
            
            var client = new ImageEmbeddingsClient(
                new Uri(azureInferenceEndpoint),
                new AzureKeyCredential(azureInferenceCredential), 
                clientOptions
            );

            AnsiConsole.MarkupLine("[green]Azure embedding client initialized[/]");
            AnsiConsole.WriteLine();

            // Step 3: Process each rendered page for embeddings
            AnsiConsole.MarkupLine($"[yellow]Processing {pageCount} pages for embeddings...[/]");
            AnsiConsole.WriteLine();

            var vectorStore = new SimpleVectorStore();

            AnsiConsole.Progress()
                .Start(ctx =>
                {
                    var task = ctx.AddTask("[green]Generating embeddings[/]", maxValue: pageCount);

                    for (int pageNumber = 1; pageNumber <= pageCount; pageNumber++)
                    {
                        var imagePath = Path.Combine(rendersDirectory, $"page_{pageNumber:D3}.png");

                        AnsiConsole.MarkupLine($"  [cyan]→[/] Processing page {pageNumber}/{pageCount}");

                        // Load image as PNG and encode as base64
                        var imageInput = ImageEmbeddingInput.Load(
                            imageFilePath: imagePath,
                            imageFormat: "png"
                        );

                        var input = new List<ImageEmbeddingInput> { imageInput };
                        var requestOptions = new ImageEmbeddingsOptions(input)
                        {
                            Model = azureInferenceModel,
                        };

                        var response = client.Embed(requestOptions);

                        //the embedding is in response.Value.Data[0].Embedding
                        var vectorEmbedding = response.Value.Data[0].Embedding;
                        var vector = vectorEmbedding.ToObjectFromJson<float[]>();

                        // Add to vector store
                        vectorStore.Add(
                            id: $"page_{pageNumber}",
                            vector: vector!,
                            metadata: new Dictionary<string, object>
                            {
                                { "pageNumber", pageNumber },
                                { "imagePath", imagePath }
                            }
                        );

                        task.Increment(1);
                    }
                });

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold green]✓[/] Successfully processed {pageCount} pages");
            AnsiConsole.WriteLine();

            // Save vector store to file
            AnsiConsole.MarkupLine($"[cyan]Saving vector database to:[/] {vectorDbPath}");
            vectorStore.SaveToFile(vectorDbPath);
            AnsiConsole.MarkupLine($"[green]✓ Vector database saved successfully[/]");
            AnsiConsole.WriteLine();

            return vectorStore;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error processing PDF:[/] {ex.Message}");
            AnsiConsole.WriteException(ex);
            throw;
        }
    }

    private static async Task PerformSearchLoopAsync(SimpleVectorStore vectorStore, string azureInferenceEndpoint, string azureInferenceCredential)
    {
        // Step 4: Text-based similarity search loop
        AnsiConsole.Write(new Rule("[bold yellow]Text-based Image Search[/]"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Enter text queries to find similar pages. Type 'exit' or 'quit' to stop.[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            var textQuery = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your [green]search query[/] (or 'exit' to quit):")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(textQuery) ||
                textQuery.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                textQuery.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[yellow]Exiting search...[/]");
                break;
            }

            try
            {
                AnsiConsole.MarkupLine($"[cyan]Searching for:[/] \"{textQuery}\"");

                // Create text embedding using the same model
                var textInput = new List<string> { textQuery };
                var textRequestOptions = new EmbeddingsOptions(textInput)
                {
                    Model = "Cohere-embed-v3-multilingual"
                };

                // Use EmbeddingsClient for text embeddings
                var embeddingsClientOptions = new AzureAIInferenceClientOptions();
                embeddingsClientOptions.AddPolicy(new ExtraParametersPolicy("drop"), HttpPipelinePosition.PerCall);
                
                var embeddingsClient = new EmbeddingsClient(
                    new Uri(azureInferenceEndpoint),
                    new AzureKeyCredential(azureInferenceCredential),
                    embeddingsClientOptions
                );

                var textResponse = embeddingsClient.Embed(textRequestOptions);

                // Convert BinaryData embedding to float array
                var embeddingBinaryData = textResponse.Value.Data[0].Embedding;
                var textVector = embeddingBinaryData.ToObjectFromJson<float[]>();

                if (textVector == null)
                {
                    AnsiConsole.MarkupLine("[red]Error: Failed to generate embedding for the query[/]");
                    continue;
                }

                // Search for similar pages
                var results = vectorStore.Search(textVector, topK: 5);

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[yellow]Top 5 most similar pages:[/]");
                AnsiConsole.WriteLine();

                var table = new Table();
                table.AddColumn("Rank");
                table.AddColumn("Page Number");
                table.AddColumn("Similarity Score");
                table.AddColumn("Image Path");

                int rank = 1;
                foreach (var (entry, similarity) in results)
                {
                    table.AddRow(
                        rank.ToString(),
                        entry.Metadata["pageNumber"].ToString()!,
                        $"{similarity:F4}",
                        Path.GetFileName(entry.Metadata["imagePath"].ToString()!)
                    );
                    rank++;
                }

                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error during search:[/] {ex.Message}");
                AnsiConsole.WriteLine();
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold green]Summary[/]"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [blue]Vectors in store:[/] {vectorStore.Count}");
        AnsiConsole.WriteLine();
    }
}
