using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Crest.Tests.Publishing;

// The publishing gate (docs/architecture.md › Standing alone, 4): no host or product name
// anywhere in the repository. The names come from outside the repository - the
// PUBLISHING_GATE_NAMES variable (comma-separated) or the file PUBLISHING_GATE_NAMES_FILE
// points at (one per line) - so the repository never contains them. Unset, the gate skips.
public sealed class PublishingGateTests
{
    [Fact]
    public void NoHostNameAppearsInTheRepository()
    {
        var names = ConfiguredNames();
        if (names.Count == 0)
        {
            Assert.Skip("No names configured (PUBLISHING_GATE_NAMES or PUBLISHING_GATE_NAMES_FILE).");
        }

        var root = RepositoryRoot();
        Assert.NotNull(root);

        var pattern = new Regex(
            @"\b(" + string.Join('|', names.Select(Regex.Escape)) + @")\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var hits = new List<string>();
        foreach (var relative in TrackedFiles(root))
        {
            var path = Path.Combine(root, relative);
            if (!File.Exists(path) || IsBinary(path))
            {
                continue;
            }

            var lineNumber = 0;
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                if (pattern.Match(line) is { Success: true } match)
                {
                    hits.Add($"{relative}:{lineNumber}: {match.Value}");
                }
            }
        }

        Assert.True(hits.Count == 0, "Host names in the repository:" + System.Environment.NewLine + string.Join(System.Environment.NewLine, hits.Take(50)));
    }

    private static List<string> ConfiguredNames()
    {
        var names = new List<string>();
        if (System.Environment.GetEnvironmentVariable("PUBLISHING_GATE_NAMES") is { Length: > 0 } inline)
        {
            names.AddRange(inline.Split(','));
        }

        if (System.Environment.GetEnvironmentVariable("PUBLISHING_GATE_NAMES_FILE") is { Length: > 0 } file && File.Exists(file))
        {
            names.AddRange(File.ReadAllLines(file));
        }

        return [.. names.Select(name => name.Trim()).Where(name => name.Length > 0 && !name.StartsWith('#')).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    // The repository root: the nearest folder above the test binaries holding Crest.slnx.
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Crest.slnx")))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    // Tracked and not-yet-tracked files git does not ignore: what a publish would carry.
    private static IEnumerable<string> TrackedFiles(string root)
    {
        var start = new ProcessStartInfo("git", "ls-files -z --cached --others --exclude-standard")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
        };

        using var git = Process.Start(start)!;
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        Assert.Equal(0, git.ExitCode);

        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool IsBinary(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[8000];
        var read = stream.Read(buffer, 0, buffer.Length);
        return Array.IndexOf(buffer, (byte)0, 0, read) >= 0;
    }
}
