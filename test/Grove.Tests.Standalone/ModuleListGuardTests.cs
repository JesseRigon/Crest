using System.Reflection;
using Grove.Modules.Manifest;
using Xunit;

namespace Grove.Tests.Standalone;

// How a module reaches the host (docs/architecture.md › Modules, features and how a module
// reaches the host): a module assembly copied into the host's output only as another module's
// dependency is never loaded, so its startups, permissions and registrations do not exist. This
// host references Grove's modules as projects, like a real host: every assembly in its output that
// is marked a module must be in its module list (the ModuleName attributes the build writes).
public sealed class ModuleListGuardTests
{
    [Fact]
    public void EveryModuleAssemblyInTheOutputIsInTheHostsModuleList()
    {
        var listed = typeof(StandaloneHost).Assembly.GetCustomAttributes<ModuleNameAttribute>()
            .Select(attribute => attribute.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(listed);

        var modules = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
            .Where(IsModule)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToArray();

        // The scan sees the modules the host lists (it is not vacuously empty).
        Assert.Contains("Grove.Contents", modules);
        Assert.Contains("Grove.OptionPicker", modules);

        var unlisted = modules.Where(name => !listed.Contains(name)).ToList();

        Assert.True(unlisted.Count == 0,
            "Module assemblies in the host's output that are not in its module list (reference them from the host, " +
            "or from a parent with PrivateAssets=\"none\"): " + string.Join(", ", unlisted.Order(StringComparer.Ordinal)));
    }

    // The marker the module build writes on every module assembly.
    private static bool IsModule(string path)
    {
        try
        {
            var assembly = Assembly.LoadFrom(path);
            return assembly.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == typeof(ModuleMarkerAttribute).FullName);
        }
        catch (Exception exception) when (exception is BadImageFormatException or FileLoadException or FileNotFoundException)
        {
            return false;
        }
    }
}
