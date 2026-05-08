using System.Diagnostics;

namespace ULinkRPC.Starter;

internal static class ProcessRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    public static void RunDotNet(string workingDirectory, string arguments)
    {
        RunProcess("dotnet", workingDirectory, arguments);
    }

    public static void RunGit(string workingDirectory, string arguments)
    {
        RunProcess("git", workingDirectory, arguments);
    }

    internal static void RunProcess(string fileName, string workingDirectory, string arguments)
    {
        RunProcessAsync(fileName, workingDirectory, arguments, DefaultTimeout, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    internal static async Task RunProcessAsync(
        string fileName,
        string workingDirectory,
        string arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
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

        using var timeoutCts = timeout is { } timeoutValue
            ? new CancellationTokenSource(timeoutValue)
            : new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            var timeoutText = timeout?.ToString() ?? DefaultTimeout.ToString();
            throw new TimeoutException(
                $"Command timed out after {timeoutText}: {fileName} {arguments}{Environment.NewLine}{stdout}{stderr}".TrimEnd());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            throw;
        }

        var stdoutResult = await stdoutTask.ConfigureAwait(false);
        var stderrResult = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Command failed: {fileName} {arguments}{Environment.NewLine}{stdoutResult}{stderrResult}".TrimEnd());
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }
}
