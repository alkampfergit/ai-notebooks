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
            ContentSHA256 = BinaryDataExtensions.CalculateSHA256(textData),
        };
        newFiles.Add(destFile, destFileDetails);
        destFileDetails.MarkProcessedBy(this);
        partition++;
        return partition;
    }
}

internal static class BinaryDataExtensions
{
    public static string CalculateSHA256(BinaryData binaryData)
    {
        byte[] byteArray = System.Security.Cryptography.SHA256.HashData(binaryData.ToMemory().Span);
        return Convert.ToHexString(byteArray).ToLowerInvariant();
    }
}
