using Azure;
using Azure.AI.DocumentIntelligence;
using Spectre.Console;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;
using SkiaSharp;
using System.Drawing;

namespace DocExtraction.Examples;

public static class AzureDocIntelligence
{
    private const string DefaultPdfPath = "C:\\temp\\manualeDreame2.pdf";

    public static async Task RunAsync()
    {
        // Load environment variables
        var endpoint = Dotenv.Get("AZURE_DI_ENDPOINT");
        var key = Dotenv.Get("AZURE_DI_KEY");

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
        {
            AnsiConsole.MarkupLine("[red]Error: AZURE_DI_ENDPOINT and AZURE_DI_KEY must be set in .env file![/]");
            return;
        }

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
        if (Path.GetExtension(filePath).ToLowerInvariant() != ".pdf")
        {
            AnsiConsole.MarkupLine("[red]Error: File is not a PDF![/]");
            return;
        }

        // Create output directory (clear if exists)
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var outputDirectory = Path.Combine(baseDirectory, "azure-doc-intelligence-output");

        if (Directory.Exists(outputDirectory))
        {
            AnsiConsole.MarkupLine($"[yellow]Clearing existing output directory...[/]");
            Directory.Delete(outputDirectory, true);
        }

        Directory.CreateDirectory(outputDirectory);

        AnsiConsole.MarkupLine($"[blue]Output directory:[/] {outputDirectory}");
        AnsiConsole.WriteLine();

        try
        {
            // Initialize Azure Document Intelligence client
            var credential = new AzureKeyCredential(key);
            var client = new DocumentIntelligenceClient(new Uri(endpoint), credential);

            AnalyzeResult result = null!;

            AnsiConsole.Status()
                .Start("Processing document with Azure Document Intelligence...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("green"));

                    // Process the document
                    using FileStream fileStream = File.OpenRead(filePath);
                    var binaryData = BinaryData.FromStream(fileStream);
                    AnalyzeDocumentOptions content = new AnalyzeDocumentOptions("prebuilt-layout", binaryData);
                    content.OutputContentFormat = DocumentContentFormat.Markdown;

                    var operation = client.AnalyzeDocument(WaitUntil.Completed, content);
                    result = operation.Value;
                });

            AnsiConsole.MarkupLine("[green]✓[/] Document analyzed successfully");
            AnsiConsole.WriteLine();

            // Display analysis results
            AnsiConsole.MarkupLine($"[yellow]Analysis Results:[/]");
            AnsiConsole.MarkupLine($"  [blue]Pages:[/] {result.Pages?.Count ?? 0}");
            AnsiConsole.MarkupLine($"  [blue]Figures found:[/] {result.Figures?.Count ?? 0}");
            AnsiConsole.MarkupLine($"  [blue]Tables:[/] {result.Tables?.Count ?? 0}");
            AnsiConsole.WriteLine();

            // Extract markdown content
            var markdownContent = result.Content ?? string.Empty;
            AnsiConsole.MarkupLine($"[blue]Markdown content length:[/] {markdownContent.Length} characters");
            AnsiConsole.WriteLine();

            // Extract tables
            var tableCount = ExtractTables(result, outputDirectory);

            // Extract and save images from figures
            var imageCount = 0;
            var imagesDirectory = Path.Combine(outputDirectory, "images");
            Directory.CreateDirectory(imagesDirectory);

            if (result.Figures != null && result.Figures.Count > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Processing {result.Figures.Count} figures...[/]");
                AnsiConsole.WriteLine();

                // Step 1: Render PDF pages that contain figures
                var renderedPagesDirectory = Path.Combine(outputDirectory, "rendered-pages");
                Directory.CreateDirectory(renderedPagesDirectory);

                var pagesWithFigures = result.Figures
                    .Where(f => f.BoundingRegions != null && f.BoundingRegions.Count > 0)
                    .Select(f => f.BoundingRegions[0].PageNumber)
                    .Distinct()
                    .ToList();

                AnsiConsole.MarkupLine($"[blue]Rendering {pagesWithFigures.Count} PDF pages with figures...[/]");

                RenderPdfPages(filePath, renderedPagesDirectory, pagesWithFigures);

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[green]✓[/] PDF pages rendered");
                AnsiConsole.WriteLine();

                // Step 2: Extract figures using bounding boxes
                AnsiConsole.Progress()
                    .Start(ctx =>
                    {
                        var task = ctx.AddTask("[green]Extracting figures[/]", maxValue: result.Figures.Count);

                        foreach (var figure in result.Figures)
                        {
                            if (figure.Id != null && figure.BoundingRegions != null && figure.BoundingRegions.Count > 0)
                            {
                                imageCount++;
                                var region = figure.BoundingRegions[0];

                                AnsiConsole.MarkupLine($"  [cyan]→[/] Processing figure {imageCount}: [white]{figure.Id}[/]");
                                AnsiConsole.MarkupLine($"    [grey]Page {region.PageNumber}, Polygon points: {region.Polygon?.Count ?? 0}[/]");

                                try
                                {
                                    var imageFileName = $"figure{imageCount}.png";
                                    var imageFilePath = Path.Combine(imagesDirectory, imageFileName);

                                    ExtractFigureFromPage(
                                        renderedPagesDirectory,
                                        region.PageNumber,
                                        region.Polygon,
                                        result.Pages[region.PageNumber - 1],
                                        imageFilePath);

                                    var fileInfo = new FileInfo(imageFilePath);
                                    AnsiConsole.MarkupLine($"    [green]✓[/] Saved as: [blue]{imageFileName}[/] ({fileInfo.Length} bytes)");
                                }
                                catch (Exception ex)
                                {
                                    AnsiConsole.MarkupLine($"    [red]✗[/] Failed: {ex.Message}");
                                }

                                AnsiConsole.WriteLine();
                            }

                            task.Increment(1);
                        }
                    });

                // Step 3: Replace all figure tags in markdown with image references
                markdownContent = ProcessFigureTags(markdownContent);
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No figures found in the document[/]");
                AnsiConsole.WriteLine();
            }

            // Save the markdown file
            var markdownFilePath = Path.Combine(outputDirectory, "extracted_content.md");
            File.WriteAllText(markdownFilePath, markdownContent);

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold green]Summary[/]"));
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold green]✓[/] Successfully processed document");
            AnsiConsole.MarkupLine($"  [blue]Markdown file:[/] {markdownFilePath}");
            AnsiConsole.MarkupLine($"  [blue]Tables extracted:[/] {tableCount}");
            AnsiConsole.MarkupLine($"  [blue]Total figures found:[/] {result.Figures?.Count ?? 0}");
            AnsiConsole.MarkupLine($"  [green]Images extracted:[/] {imageCount}");
            AnsiConsole.MarkupLine($"  [blue]Output directory:[/] {outputDirectory}");
            AnsiConsole.WriteLine();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error processing document:[/] {ex.Message}");
            AnsiConsole.WriteException(ex);
        }
    }

    private static int ExtractTables(AnalyzeResult result, string outputDirectory)
    {
        if (result.Tables == null || result.Tables.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No tables found in the document[/]");
            AnsiConsole.WriteLine();
            return 0;
        }

        // Create tables subdirectory
        var tablesDirectory = Path.Combine(outputDirectory, "tables");
        Directory.CreateDirectory(tablesDirectory);

        AnsiConsole.MarkupLine($"[yellow]Processing {result.Tables.Count} tables...[/]");
        AnsiConsole.WriteLine();

        int tableIndex = 0;
        foreach (var table in result.Tables)
        {
            tableIndex++;
            int pageNumber = table.BoundingRegions?[0].PageNumber ?? 0;

            AnsiConsole.MarkupLine($"  [cyan]→[/] Table {tableIndex} on page {pageNumber}: {table.RowCount} rows × {table.ColumnCount} columns");

            // Build markdown table
            var sb = new StringBuilder();
            sb.AppendLine($"# Table {tableIndex} - Page {pageNumber}");

            if (!string.IsNullOrEmpty(table.Caption?.Content))
            {
                sb.AppendLine($"**Caption:** {table.Caption.Content}");
            }
            sb.AppendLine($"**Rows:** {table.RowCount}, **Columns:** {table.ColumnCount}");
            sb.AppendLine();

            // Create a 2D array to hold all cell contents
            string[,] tableData = new string[table.RowCount, table.ColumnCount];

            // Fill the array with cell contents
            foreach (var cell in table.Cells)
            {
                string content = cell.Content?.Trim() ?? "";
                // Escape markdown characters and handle line breaks
                content = content.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
                tableData[cell.RowIndex, cell.ColumnIndex] = content;
            }

            // Generate markdown table
            for (int row = 0; row < table.RowCount; row++)
            {
                var rowCells = new string[table.ColumnCount];
                for (int col = 0; col < table.ColumnCount; col++)
                {
                    rowCells[col] = tableData[row, col] ?? "";
                }

                sb.AppendLine("| " + string.Join(" | ", rowCells) + " |");

                // Add separator after header row
                if (row == 0)
                {
                    sb.AppendLine("|" + string.Concat(Enumerable.Repeat(" --- |", table.ColumnCount)));
                }
            }

            // Save table to file in tables subdirectory
            var tableFileName = $"table_{tableIndex:D2}_page{pageNumber}.md";
            var tableFilePath = Path.Combine(tablesDirectory, tableFileName);
            File.WriteAllText(tableFilePath, sb.ToString(), Encoding.UTF8);

            AnsiConsole.MarkupLine($"    [green]✓[/] Saved as: [blue]tables/{tableFileName}[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓[/] {tableIndex} tables extracted");
        AnsiConsole.WriteLine();

        return tableIndex;
    }

    private static void RenderPdfPages(string pdfPath, string outputDirectory, List<int> pageNumbers)
    {
        using var document = PdfDocument.Open(pdfPath);
        document.AddSkiaPageFactory();

        foreach (var pageNumber in pageNumbers)
        {
            var outputFileName = Path.Combine(outputDirectory, $"page_{pageNumber}.png");
            using var fs = new FileStream(outputFileName, FileMode.Create);
            using var ms = document.GetPageAsPng(pageNumber, 2, 100);
            ms.WriteTo(fs);
        }
    }

    private static void ExtractFigureFromPage(
        string renderedPagesDirectory,
        int pageNumber,
        IReadOnlyList<float> polygon,
        DocumentPage page,
        string outputFilePath)
    {
        var pageImagePath = Path.Combine(renderedPagesDirectory, $"page_{pageNumber}.png");

        using var bitmap = SKBitmap.Decode(pageImagePath);

        // Convert polygon points from document coordinates to image coordinates
        List<PointF> absolutePoints = [];
        for (int i = 0; i < polygon.Count; i += 2)
        {
            float x = polygon[i] / page.Width!.Value * bitmap.Width;
            float y = polygon[i + 1] / page.Height!.Value * bitmap.Height;
            absolutePoints.Add(new PointF(x, y));
        }

        // Calculate bounding rectangle
        float minX = absolutePoints.Min(p => p.X);
        float maxX = absolutePoints.Max(p => p.X);
        float minY = absolutePoints.Min(p => p.Y);
        float maxY = absolutePoints.Max(p => p.Y);
        float width = maxX - minX;
        float height = maxY - minY;

        // Extract the figure region
        var rect = new SKRectI((int)minX, (int)minY, (int)(minX + width), (int)(minY + height));
        using var croppedBitmap = new SKBitmap((int)width, (int)height);

        bitmap.ExtractSubset(croppedBitmap, rect);

        // Save the cropped figure
        using var image = SKImage.FromBitmap(croppedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(outputFilePath);
        data.SaveTo(stream);
    }

    private static string ProcessFigureTags(string markdown)
    {
        // Process all <figure> tags and replace them with markdown image references
        // Azure Document Intelligence outputs figures as HTML <figure> tags
        // This matches the approach from the original notebook example

        var figurePattern = @"<figure>(?<content>.*?)</figure>";
        var regex = new Regex(figurePattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        int figureCount = 0;
        var processedContent = regex.Replace(markdown, match =>
        {
            figureCount++;
            var figureContent = match.Groups["content"].Value.Trim();

            return $@"
### Figure {figureCount}
![Figure {figureCount}](images/figure{figureCount}.png)

{figureContent}

### End Figure {figureCount}";
        });

        return processedContent;
    }
}
