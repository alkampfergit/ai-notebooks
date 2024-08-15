using System.Diagnostics;
using System.IO;
using System.Threading;

public class PythonWrapper
{
    private readonly string python3Location;

    public PythonWrapper(string python3location)
    {
        python3Location = python3location;
    }

    public async Task<string> Execute(string scriptPath, string arguments = "")
    {
        if (!File.Exists(python3Location))
        {
            Console.WriteLine($"Python3 not found at {python3Location}");
            throw new FileNotFoundException($"Python3 not found at {python3Location}");
        }

        if (!File.Exists(scriptPath))
        {
            Console.WriteLine($"Script not found at {scriptPath}");
            throw new FileNotFoundException($"Script not found at {scriptPath}");
        }
        
        ProcessStartInfo start = new ProcessStartInfo();
        start.FileName = python3Location;
        start.Arguments = $"{scriptPath} {arguments}"; // Add the arguments to the command line
        
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.CreateNoWindow = true;

        using Process process = Process.Start(start);

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
            string exceptionCode = $"Unable to extract text: Error:\n{error}";
            Console.WriteLine(exceptionCode);
            throw new Exception(exceptionCode);
        }

        Console.WriteLine("Output: {0}", output);

        return output;
    }
}
