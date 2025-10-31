using System.Diagnostics;
using System.Text;
using CliWrap;

namespace PointlessWaymarks.CommonTools;

public static class ProcessTools
{
    public static async Task<(bool success, string standardOutput, string errorOutput)> Execute(string programToExecute,
        string executionParameters, IProgress<string>? progress)
    {
        return await Execute(programToExecute, executionParameters, string.Empty, progress);
    }

    public static async Task<(bool success, string standardOutput, string errorOutput)> Execute(string programToExecute,
        string executionParameters, string workingDirectory, IProgress<string>? progress)
    {
        if (string.IsNullOrWhiteSpace(programToExecute)) return (false, string.Empty, "Blank program to Execute?");

        var programToExecuteFile = new FileInfo(programToExecute);

        if (!programToExecuteFile.Exists)
            return (false, string.Empty, $"Program to Execute {programToExecuteFile} does not exist.");

        var standardOutput = new StringBuilder();
        var errorOutput = new StringBuilder();

        progress?.Report($"Setting up execution of {programToExecute} {executionParameters}");

        using var forcefulCts = new CancellationTokenSource();
        using var gracefulCts = new CancellationTokenSource();
        gracefulCts.CancelAfter(TimeSpan.FromSeconds(180));
        forcefulCts.CancelAfter(TimeSpan.FromSeconds(190));

        try
        {
            progress?.Report("Starting Process");

            var cliCommand = Cli.Wrap(programToExecuteFile.FullName)
                .WithArguments(executionParameters)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(x =>
                {
                    standardOutput.AppendLine(x);
                    progress?.Report(x);
                }));

            if (!string.IsNullOrWhiteSpace(workingDirectory)) cliCommand.WithWorkingDirectory(workingDirectory);

            var commandResult = await cliCommand.ExecuteAsync(forcefulCts.Token, gracefulCts.Token);

            return (commandResult.IsSuccess, standardOutput.ToString(), errorOutput.ToString());
        }
        catch (Exception e)
        {
            progress?.Report($"Error Running Process: {e.Message}");
        }

        return (false, standardOutput.ToString(), errorOutput.ToString());
    }

    // Variation that streams both stdout and stderr lines to the provided IProgress<string> as they are emitted.
    public static async Task<(bool success, string standardOutput, string errorOutput)> ExecuteStreamed(
        string programToExecute, string executionParameters, string workingDirectory, IProgress<string>? progress)
    {
        if (string.IsNullOrWhiteSpace(programToExecute)) return (false, string.Empty, "Blank program to Execute?");

        var programToExecuteFile = new FileInfo(programToExecute);

        if (!programToExecuteFile.Exists)
            return (false, string.Empty, $"Program to Execute {programToExecuteFile} does not exist.");

        var standardOutput = new StringBuilder();
        var errorOutput = new StringBuilder();
        var outputLock = new object();

        progress?.Report($"Setting up execution (streamed) of {programToExecute} {executionParameters}");

        using var forcefulCts = new CancellationTokenSource();
        using var gracefulCts = new CancellationTokenSource();
        gracefulCts.CancelAfter(TimeSpan.FromSeconds(180));
        forcefulCts.CancelAfter(TimeSpan.FromSeconds(190));

        try
        {
            progress?.Report("Starting Process (streamed)");

            var cliCommand = Cli.Wrap(programToExecuteFile.FullName)
                .WithArguments(executionParameters)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
                {
                    lock (outputLock)
                    {
                        standardOutput.AppendLine(line);
                    }

                    // Report stdout lines as they come
                    progress?.Report(line);
                }))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                {
                    lock (outputLock)
                    {
                        errorOutput.AppendLine(line);
                    }

                    // Prefix stderr so callers can differentiate if they want
                    progress?.Report($"ERR: {line}");
                }));

            if (!string.IsNullOrWhiteSpace(workingDirectory))
                cliCommand = cliCommand.WithWorkingDirectory(workingDirectory);

            var commandResult = await cliCommand.ExecuteAsync(forcefulCts.Token, gracefulCts.Token);

            return (commandResult.IsSuccess, standardOutput.ToString(), errorOutput.ToString());
        }
        catch (Exception e)
        {
            progress?.Report($"Error Running Process (streamed): {e.Message}");
            lock (outputLock)
            {
                errorOutput.AppendLine(e.ToString());
            }
        }

        return (false, standardOutput.ToString(), errorOutput.ToString());
    }


    public static void Open(string fileName)
    {
        var ps = new ProcessStartInfo(fileName) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }
}