using DocExtraction.Examples;
using Spectre.Console;

while (true)
{
    AnsiConsole.Clear();

    // Display title
    AnsiConsole.Write(
        new FigletText("Doc Extraction")
            .LeftJustified()
            .Color(Color.Blue));

    AnsiConsole.WriteLine();

    // Show menu
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[green]Select an example:[/]")
            .PageSize(10)
            .AddChoices(new[] {
                "Hello World Example",
                "PDF Image Extraction",
                "LLM Image Description (GPT based)",
                "Local LLM Image Description (Gemma via Semantic Kernel)",
                "Azure Document Intelligence",
                "PDF Embedding (Render + Azure Embeddings)",
                "Exit"
            }));

    AnsiConsole.WriteLine();

    // Handle selection
    switch (choice)
    {
        case "Hello World Example":
            HelloWorldExample.Run();
            break;
        case "PDF Image Extraction":
            PdfImageExtraction.Run();
            break;
        case "LLM Image Description (GPT based)":
            await LlmImageDescription.RunAsync();
            break;
        case "Local LLM Image Description (Gemma via Semantic Kernel)":
            await LocalLlmImageDescription.RunAsync();
            break;
        case "Azure Document Intelligence":
            await AzureDocIntelligence.RunAsync();
            break;
        case "PDF Embedding (Render + Azure Embeddings)":
            await PdfEmbeddingExample.RunAsync();
            break;
        case "Exit":
            AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
            return;
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[grey]Press any key to return to menu...[/]");
    Console.ReadKey(true);
}
