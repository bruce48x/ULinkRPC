using System.Diagnostics;

namespace ULinkRPC.Starter;

internal static class ProcessRunner
{
    public static void RunDotNet(string workingDirectory, string arguments)
    {
        RunProcess("dotnet", workingDirectory, arguments);
    }

    public static void RunGit(string workingDirectory, string arguments)
    {
        RunProcess("git", workingDirectory, arguments);
    }

    private static void RunProcess(string fileName, string workingDirectory, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });

        if (process is null)
        {
            throw new InvalidOperationException($"Failed to start '{fileName} {arguments}'.");
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Command failed: {fileName} {arguments}{Environment.NewLine}{stdout}{stderr}".TrimEnd());
    }
}
