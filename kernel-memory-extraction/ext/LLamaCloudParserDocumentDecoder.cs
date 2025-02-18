using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Context;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class LLamaCloudParserDocumentDecoder : IContentDecoder
{
    private readonly ILogger<LLamaCloudParserDocumentDecoder> _log;
    private readonly LLamaCloudParserClient _client;
    private readonly IContextProvider _contextProvider;

    public LLamaCloudParserDocumentDecoder(
        LLamaCloudParserClient client,
        IContextProvider contextProvider,
        ILoggerFactory? loggerFactory = null)
    {
        _log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<LLamaCloudParserDocumentDecoder>();
        _client = client;
        _contextProvider = contextProvider;
    }

    /// <inheritdoc />
    public bool SupportsMimeType(string mimeType)
    {
        //Here we can add more mime types
        return mimeType != null
            && (
                mimeType.StartsWith(MimeTypes.Pdf, StringComparison.OrdinalIgnoreCase)
                || mimeType.StartsWith(MimeTypes.OpenDocumentText, StringComparison.OrdinalIgnoreCase)
            );
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(filename);
        return this.DecodeAsync(stream, filename, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = default)
    {
        using var stream = data.ToStream();
        return DecodeAsync(stream, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default)
    {
        return DecodeAsync(data, null, cancellationToken);
    }

    public async Task<FileContent> DecodeAsync(Stream data, string? fileName, CancellationToken cancellationToken = default)
    {
        _log.LogDebug("Extracting structured text with llamacloud from file");

        //retrieve filename and parsing instructions from context
        var context = _contextProvider.GetContext();
        string parsingInstructions = string.Empty;
        if (context.Arguments.TryGetValue(LLamaCloudParserDocumentDecoderExtensions.FileNameKey, out var fileNameContext))
        {
            fileName = fileNameContext as string ?? string.Empty;
        }
        if (context.Arguments.TryGetValue(LLamaCloudParserDocumentDecoderExtensions.ParsingInstructionsKey, out var parsingInstructionsContext))
        {
            parsingInstructions = parsingInstructionsContext as string ?? string.Empty;
        }

        // ok we need a way to find the correct instruction for the file, so we can use a different configuration
        // for each file that we are going to parse.
        var parameters = new UploadParameters();

        //file name must not be null
        if (string.IsNullOrEmpty(fileName))
        {
            throw new Exception("LLAMA Cloud error: file name is missing");
        }

        //ok we need a temporary file name that we need to use to upload the file, we need a seekable stream
        var tempFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_" + fileName);
        using (var writeFileStream = File.Create(tempFileName))
        {
            await data.CopyToAsync(writeFileStream, cancellationToken);
        }

        Console.WriteLine($"Uploading file {fileName} to LLAMA Cloud with instructions {parsingInstructions}");
        parameters.WithParsingInstructions(parsingInstructions);

        var response = await PerformCall(fileName, tempFileName, parameters);

        if (response != null && response.ErrorCode != null)
        {
            throw new Exception($"LLAMA Cloud error: {response.ErrorCode} - {response.ErrorMessage}");
        }

        if (response == null)
        {
            throw new Exception("LLAMA Cloud error: no response");
        }

        var jobId = response.Id.ToString();

        //now wait for the job to be completed
        var jobResponse = await _client.WaitForJobSuccessAsync(jobId, TimeSpan.FromMinutes(5));

        if (!jobResponse)
        {
            throw new Exception("LLAMA Cloud error: job not completed");
        }

        // ok now the job is completed, we can get the markdown
        var markdown = await _client.GetJobRawMarkdownAsync(jobId);

        var result = new FileContent(MimeTypes.MarkDown);
        var section = new FileSection(1, markdown, false);
        result.Sections.Add(section);

        return result;
    }

    private async Task<UploadResponse> PerformCall(
        string fileName,
        string physicalTempFileName,
        UploadParameters parameters)
    {
        try
        {
            await using var tempFileStream = File.OpenRead(physicalTempFileName);
            var uploadResponse = await _client.UploadAsync(tempFileStream, fileName, parameters);
            return uploadResponse;
        }
        finally
        {
            File.Delete(physicalTempFileName);
        }
    }
}

public static class LLamaCloudParserDocumentDecoderExtensions
{
    public const string FileNameKey = "llamacloud.filename";
    public const string ParsingInstructionsKey = "llamacloud.parsing_instructions";

    public static void AddLLamaCloudParserOptions(IContextProvider contextProvider, string filename, string parseInstructions)
    {
        contextProvider.SetContextArg(FileNameKey, filename);
        contextProvider.SetContextArg(ParsingInstructionsKey, parseInstructions);
    }
}

public sealed class CustomSamplePartitioningHandler : IPipelineStepHandler
{
    private readonly IPipelineOrchestrator _orchestrator;
    private readonly ILogger<TextPartitioningHandler> _log;

    /// <inheritdoc />
    public string StepName { get; }

    /// <summary>
    /// Handler responsible for partitioning text in small chunks.
    /// Note: stepName and other params are injected with DI.
    /// </summary>
    /// <param name="stepName">Pipeline step for which the handler will be invoked</param>
    /// <param name="orchestrator">Current orchestrator used by the pipeline, giving access to content and other helps.</param>
    /// <param name="options">The customize text partitioning option</param>
    /// <param name="loggerFactory">Application logger factory</param>
    public CustomSamplePartitioningHandler(
        string stepName,
        IPipelineOrchestrator orchestrator,
        ILoggerFactory? loggerFactory = null)
    {
        this.StepName = stepName;
        this._orchestrator = orchestrator;

        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<TextPartitioningHandler>();
        this._log.LogInformation("Handler '{0}' ready", stepName);
    }

    /// <inheritdoc />
    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(
        DataPipeline pipeline, CancellationToken cancellationToken = default)
    {
        this._log.LogDebug("Markdown question Partitioning text, pipeline '{0}/{1}'", pipeline.Index, pipeline.DocumentId);

        if (pipeline.Files.Count == 0)
        {
            this._log.LogWarning("Pipeline '{0}/{1}': there are no files to process, moving to next pipeline step.", pipeline.Index, pipeline.DocumentId);
            return (ReturnType.Success, pipeline);
        }

        var context = pipeline.GetContext();

        foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
        {
            // Track new files being generated (cannot edit originalFile.GeneratedFiles while looping it)
            Dictionary<string, DataPipeline.GeneratedFileDetails> newFiles = [];

            foreach (KeyValuePair<string, DataPipeline.GeneratedFileDetails> generatedFile in uploadedFile.GeneratedFiles)
            {
                var file = generatedFile.Value;
                if (file.AlreadyProcessedBy(this))
                {
                    this._log.LogTrace("File {0} already processed by this handler", file.Name);
                    continue;
                }

                // Partition only the original text
                if (file.ArtifactType != DataPipeline.ArtifactTypes.ExtractedText)
                {
                    this._log.LogTrace("Skipping file {0} (not original text)", file.Name);
                    continue;
                }

                // Use a different partitioning strategy depending on the file type
                BinaryData partitionContent = await this._orchestrator.ReadFileAsync(pipeline, file.Name, cancellationToken).ConfigureAwait(false);
                string partitionsMimeType = MimeTypes.MarkDown;

                // Skip empty partitions. Also: partitionContent.ToString() throws an exception if there are no bytes.
                if (partitionContent.IsEmpty) { continue; }
                int partition = 1;
                switch (file.MimeType)
                {
                    case MimeTypes.MarkDown:
                        {
                            this._log.LogDebug("Partitioning MarkDown file {0}", file.Name);
                            string content = partitionContent.ToString();
                            partitionsMimeType = MimeTypes.MarkDown;

                            var sb = new StringBuilder(1024);
                            using (var reader = new StringReader(content))
                            {
                                string? line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (string.IsNullOrWhiteSpace(line))
                                    {
                                        continue;
                                    }

                                    if (line.StartsWith("---"))
                                    {
                                        partition = await AddSegment(pipeline, uploadedFile, newFiles, partitionsMimeType, partition, sb, cancellationToken).ConfigureAwait(false);
                                        sb.Clear();
                                        continue;
                                    }

                                    sb.AppendLine(line);
                                }
                            }

                            // Write remaining content if any
                            if (sb.Length > 0)
                            {
                                await AddSegment(pipeline, uploadedFile, newFiles, partitionsMimeType, partition, sb, cancellationToken).ConfigureAwait(false);
                            }

                            break;
                        }

                    default:
                        this._log.LogWarning("File {0} cannot be partitioned, type '{1}' not supported", file.Name, file.MimeType);
                        // Don't partition other files
                        continue;
                }
            }

            // Add new files to pipeline status
            foreach (var file in newFiles)
            {
                uploadedFile.GeneratedFiles.Add(file.Key, file.Value);
            }
        }

        return (ReturnType.Success, pipeline);
    }

    private async Task<int> AddSegment(DataPipeline pipeline, DataPipeline.FileDetails uploadedFile, Dictionary<string, DataPipeline.GeneratedFileDetails> newFiles, string partitionsMimeType, int partition, StringBuilder sb, CancellationToken cancellationToken)
    {
        if (sb.Length == 0)
        {
            //do not increment partition, an empty segment is not a segment.
            return partition;
        }

        var text = sb.ToString().Trim('\n', '\r', ' ');

        //is empty after trimming?
        if (string.IsNullOrWhiteSpace(text))
        {
            //do not increment partition, an empty segment is not a segment.
            return partition;
        }

        var destFile = uploadedFile.GetPartitionFileName(partition);
        var textData = new BinaryData(sb.ToString());
        await this._orchestrator.WriteFileAsync(pipeline, destFile, textData, cancellationToken).ConfigureAwait(false);

        var destFileDetails = new DataPipeline.GeneratedFileDetails
        {
            Id = Guid.NewGuid().ToString("N"),
            ParentId = uploadedFile.Id,
            Name = destFile,
            Size = sb.Length,
            MimeType = partitionsMimeType,
            ArtifactType = DataPipeline.ArtifactTypes.TextPartition,
            PartitionNumber = partition,
            SectionNumber = 1,
            Tags = pipeline.Tags,
            ContentSHA256 = textData.CalculateSHA256(),
        };
        newFiles.Add(destFile, destFileDetails);
        destFileDetails.MarkProcessedBy(this);
        partition++;
        return partition;
    }
}
