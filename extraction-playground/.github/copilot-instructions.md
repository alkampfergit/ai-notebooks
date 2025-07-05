# DocExtraction - AI Agent Instructions

## Project Overview
.NET 9 console application for document extraction and processing workflows. Menu-driven architecture using Spectre.Console for interactive CLI. Focus on PDF processing, Azure Document Intelligence integration, image extraction, and vector embeddings.

## Architecture Pattern: Example-Based Menu System

All functionality lives in `Examples/` folder as static classes with `Run()` or `RunAsync()` methods:
- Each example class = one menu option in `Program.cs`
- Pattern: `public static class ExampleName` with `public static [async Task] Run[Async]()`
- Examples are self-contained demos, not production code modules
- Menu uses `SelectionPrompt<string>` with exact string matching in switch statement

**Adding new examples:**
1. Create class in `Examples/` folder following naming: `MyFeatureExample.cs`
2. Implement `public static async Task RunAsync()` or `public static void Run()`
3. Add menu choice to `SelectionPrompt.AddChoices()` array in `Program.cs`
4. Add corresponding case to switch statement with exact string match

## Key Dependencies & Usage Patterns

### PDF Processing (PdfPig + SkiaSharp)
```csharp
using var document = PdfDocument.Open(filePath);
document.AddSkiaPageFactory(); // REQUIRED before rendering
var pageCount = document.NumberOfPages;
// Render: document.GetPageAsPng(pageNumber, scale: 2, quality: 100)
```
Always call `AddSkiaPageFactory()` after opening document. Scale=2 gives good quality for embeddings/OCR.

### Azure Document Intelligence
- Uses `prebuilt-layout` model with markdown output format
- Extracts: text content, tables, figures with bounding boxes
- Pattern: Extract figure bounding boxes → render PDF pages → crop figures from rendered images
- Figures extracted as: `page_{N}.png` → crop using polygon coordinates → save as `figure{N}.png`

### Azure AI Inference (Embeddings)
```csharp
var client = new ImageEmbeddingsClient(
    new Uri(Environment.GetEnvironmentVariable("AZURE_INFERENCE_ENDPOINT")),
    new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_INFERENCE_CREDENTIAL"))
);
```
Environment variables checked at runtime; examples gracefully skip if not configured.

### Configuration: Custom Dotenv Implementation
**Never use external dotenv libraries.** Project includes `dotenv.cs` with static `Dotenv.Get(key)` method:
- Searches upward from current directory for `.env` file
- Falls back to `Environment.GetEnvironmentVariable()`
- Case-insensitive key matching
- Usage: `var endpoint = Dotenv.Get("AZURE_DI_ENDPOINT");`

See `.env.example` for required keys. Never hardcode credentials.

## Output Structure Convention
Each example creates its own output directory in `bin/Debug/net9.0/`:
- `pdf-image-extraction/` - rendered pages
- `azure-doc-intelligence-output/` - markdown + extracted images + tables
- `pdf-embedding-output/renders/` - rendered pages for embedding
- Pattern: `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output-name")`

## UI Patterns with Spectre.Console

**Progress bars for multi-step operations:**
```csharp
AnsiConsole.Progress().Start(ctx => {
    var task = ctx.AddTask("[green]Description[/]", maxValue: totalItems);
    // ... work
    task.Increment(1);
});
```

**Status spinner for single async operations:**
```csharp
AnsiConsole.Status().Start("Processing...", ctx => {
    ctx.Spinner(Spinner.Known.Dots);
    ctx.SpinnerStyle(Style.Parse("green"));
    // ... work
});
```

**File path prompts with defaults:**
```csharp
var filePath = AnsiConsole.Prompt(
    new TextPrompt<string>("Enter the [green]PDF file path[/]:")
        .DefaultValue(defaultPath)
        .ShowDefaultValue()
);
```

Always validate file existence and extension before processing.

## Vector Store Pattern
`SimpleVectorStore.cs` provides in-memory cosine similarity search:
- Add vectors: `Add(id, float[] vector, metadata)`
- Search: `Search(float[] queryVector, topK)`
- Persistence: `SaveToFile()` / `LoadFromFile()` (JSON format)
- Used for semantic search over document embeddings

## Development Commands
```bash
cd DocExtraction
dotnet build
dotnet run
```
No test projects currently. Examples serve as integration tests.

## Common Patterns to Follow

**File naming for sequences:** Use zero-padded numbers: `page_{pageNumber:D3}.png`

**Error handling:** Wrap main operations in try-catch, use `AnsiConsole.MarkupLine("[red]Error:...[/]")` for errors

**Image coordinate conversion:** Azure returns normalized coords (0-1); convert to pixel coords:
```csharp
float x = polygon[i] / page.Width!.Value * bitmap.Width;
```

**Async methods:** Use `async Task RunAsync()` for Azure SDK calls; add `await` to menu case in Program.cs

## What NOT to Do
- Don't add test projects (examples are the tests)
- Don't create shared utility classes (keep examples self-contained)
- Don't use external configuration libraries (use built-in Dotenv)
- Don't hardcode file paths (use prompts with defaults)
- Don't create subdirectories in Examples/ (flat structure)
