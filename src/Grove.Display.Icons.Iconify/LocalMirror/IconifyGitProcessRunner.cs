using System.Diagnostics;

namespace Grove.Display.Icons.Iconify;

/// <summary>Runs the git executable on the host.</summary>
public sealed class IconifyGitProcessRunner : IIconifyGitRunner
{
    public async Task RunAsync(string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start git.");
        }

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var output = ((await stdout) + Environment.NewLine + (await stderr)).Trim();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(output) ? $"git {arguments} failed with exit code {process.ExitCode}." : output);
        }
    }
}
