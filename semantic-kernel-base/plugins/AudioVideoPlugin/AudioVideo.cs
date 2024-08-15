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
    public AudioVideoPlugin(
        AudioVideoPluginConfig config, 
        PythonWrapper pythonWrapper = null,
        ILogger logger = null)
    {
        this._logger = logger ?? NullLogger.Instance;
        this._pythonWrapper = pythonWrapper;
    }

    private ILogger _logger;
    private PythonWrapper _pythonWrapper;

    [KernelFunction, Description("extract audio in wav format from an mp4 file")]
    public async Task<string> ExtractAudio(
        [Description("Full path to the mp4 file")] string videofile,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Calling plugin with videofile {0}", videofile);
        _logger.LogDebug("Extracting audio file from video {videofile}", videofile);
        // First of all, change the extension of the video file to create the output path
        string audioPath = videofile.Replace(".mp4", ".wav", StringComparison.OrdinalIgnoreCase);

        if (!File.Exists(videofile))
        {
            string exceptionCode = $"Video file {videofile} does not exist";
            Console.WriteLine(exceptionCode);
            throw new Exception(exceptionCode);
        }

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
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            //Create a cancellation token for 30 seconds
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(cancellationToken: cts.Token).ConfigureAwait(false);

            //now that the process exited I can read the output tasks
            string output = await outputTask.ConfigureAwait(false);
            string error = await errorTask.ConfigureAwait(false);

            //need to check if the exit code is ok.
            if (process.ExitCode != 0)
            {
                string exceptionCode = $"Unable to extract audio with ffmpeg\n\nOutput:\n{output}\n\nError:\n{error}";
                Console.WriteLine(exceptionCode);
                throw new Exception(exceptionCode);
            }

            Console.WriteLine("Output: {0}", output);
        }

        // Now ffmpeg has created the audio file, return the path to it
        _logger.LogInformation("Audio file extracted to {audioPath}", audioPath);
        return audioPath;
    }

    [KernelFunction, Description("Transcript audio from a wav file to a timeline extracting a transcript")]
    [return: Description("Transcript of an audio file with time markers")]
    public async Task<string> TranscriptTimeline([Description("Full path to the wav file")] string audioFile)
    {
        // find the location of current script

        var script = Path.Combine(Environment.CurrentDirectory, "..", "python", "transcript_timeline.py");
        Console.WriteLine("Transcripting audio file {0} with script {1}", audioFile, script);
        var result = await this._pythonWrapper.Execute(script, audioFile);
        return result;
    }
}
