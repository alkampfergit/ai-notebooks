using Spectre.Console;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;

namespace DocExtraction.Examples;

public static class PdfImageExtraction
{
    public static void Run()
    {
        // Ask user for PDF file path with default value
        var defaultPath = "C:\\temp\\manualedreame2.pdf";
        var filePath = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter the [green]PDF file path[/]:")
                .DefaultValue(defaultPath)
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

        // Create output directory
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var outputDirectory = Path.Combine(baseDirectory, "pdf-image-extraction");
        Directory.CreateDirectory(outputDirectory);

        AnsiConsole.MarkupLine($"[blue]Output directory:[/] {outputDirectory}");
        AnsiConsole.WriteLine();

        try
        {
            using (var document = PdfDocument.Open(filePath))
            {
                document.AddSkiaPageFactory();

                var pageCount = document.NumberOfPages;
                AnsiConsole.MarkupLine($"[green]Processing {pageCount} pages...[/]");
                AnsiConsole.WriteLine();

                // Process all pages with progress bar
                AnsiConsole.Progress()
                    .Start(ctx =>
                    {
                        var task = ctx.AddTask("[green]Extracting pages[/]", maxValue: pageCount);

                        for (int pageNumber = 1; pageNumber <= pageCount; pageNumber++)
                        {
                            var outputFileName = Path.Combine(outputDirectory, $"page_{pageNumber:D3}.png");

                            using (var fs = new FileStream(outputFileName, FileMode.Create))
                            using (var ms = document.GetPageAsPng(pageNumber, 1, 75))
                            {
                                ms.WriteTo(fs);
                            }

                            task.Increment(1);
                        }
                    });

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[bold green]✓[/] Successfully extracted {pageCount} pages to:");
                AnsiConsole.MarkupLine($"  [blue]{outputDirectory}[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error processing PDF:[/] {ex.Message}");
        }
    }
}
