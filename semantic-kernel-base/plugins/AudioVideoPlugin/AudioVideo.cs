using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public record AudioVideoPluginConfig(string Configuration);

[Description("Audio and video plugin for the Semantic Kernel")] 
public class AudioVideoPlugin
{
    public AudioVideoPlugin(AudioVideoPluginConfig config, ILogger logger = null)
    {
        this._logger = logger ?? NullLogger.Instance;
    }

    private ILogger _logger;

    [KernelFunction, Description("extract audio in wav format from an mp4 file")]
    public string ExtractAudio([Description("Full path to the mp4 file")] string videofile)
    {
        _logger.LogDebug("Extracting audio file from video {videofile}", videofile);
        // First of all, change the extension of the video file to create the output path
        string audioPath = videofile.Replace(".mp4", ".wav", StringComparison.OrdinalIgnoreCase);

        // If the audio file exists, delete it, maybe it is an old version
        if (File.Exists(audioPath))
        {
             _logger.LogDebug("Deleting existing audio file {videofile}", audioPath);
            File.Delete(audioPath);
        }

        string command = $"-i {videofile} -vn -acodec pcm_s16le -ar 44100 -ac 2 {audioPath}";

        _logger.LogInformation("Running ffmpeg with command {command}", command);
        Console.WriteLine("Running ffmpeg with command {0}", command);
        using (var process = new Process())
        {
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments = command;
            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.RedirectStandardError = false;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;

            process.Start();
            process.WaitForExit();
        }

        // Now ffmpeg has created the audio file, return the path to it
        _logger.LogInformation("Audio file extracted to {audioPath}", audioPath);
        return audioPath;
    }

    [KernelFunction, Description("Transcript audio from a wav file to a timeline extracting a transcript")]
    [return: Description("Transcript of an audio file with time markers")]
    public string TranscriptTimeline([Description("Full path to the wav file")] string audioFile)
    {
        Console.WriteLine("Transcripting audio file {0}", audioFile);
        var python = new PythonWrapper(@"C:\develop\github\SemanticKernelPlayground\skernel\Scripts\python.exe");
        var script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python", "transcript_timeline.py");
        var result = python.Execute(script, audioFile);
        return result;
    }
}
