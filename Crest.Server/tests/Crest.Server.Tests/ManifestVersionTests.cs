using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Crest.Server.Tests;

// The Crest packages version as <Crest compatibility>.<security patch>.<bug patch>
// (CrestVersion in Crest.csproj). A module manifest that carries that five-part
// scheme must show the same number, since the themes page displays the manifest's Version.
public partial class ManifestVersionTests
{
    [Fact]
    public void FivePartManifestVersionsMatchTheCrestVersion()
    {
        var root = RepositoryRoot();
        var crestVersion = CrestVersion(Path.Combine(root, "Crest.Server", "Crest.Server.csproj"));

        var manifests = Directory.EnumerateFiles(root, "Manifest.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(path => (Path: Path.GetRelativePath(root, path), Version: ManifestVersion().Match(File.ReadAllText(path))))
            .Where(manifest => manifest.Version.Success && manifest.Version.Groups["version"].Value.Count(c => c == '.') == 4)
            .ToArray();

        Assert.NotEmpty(manifests);
        Assert.All(manifests, manifest => Assert.True(
            manifest.Version.Groups["version"].Value == crestVersion,
            $"{manifest.Path} declares {manifest.Version.Groups["version"].Value}; the Crest version is {crestVersion}."));
    }

    private static string CrestVersion(string projectPath)
    {
        var project = File.ReadAllText(projectPath);
        string Property(string name) => Regex.Match(project, $"<{name}>(?<value>[^<]+)</{name}>").Groups["value"].Value;
        return $"{Property("PlatformCompatibilityVersion")}.{Property("CrestSecurityPatch")}.{Property("CrestBugPatch")}";
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj" or "node_modules");

    private static string RepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Crest.Tests.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the Crest repository root.");
    }

    [GeneratedRegex("""Version\s*=\s*"(?<version>[0-9.]+)"\s*,?""")]
    private static partial Regex ManifestVersion();
}
