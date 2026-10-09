using System.Reflection;
using Microsoft.Extensions.Hosting;
using Crest.Modules.Manifest;

namespace Crest.Modules;

public class AssemblyAttributeModuleNamesProvider : IModuleNamesProvider
{
    private readonly List<string> _moduleNames;

    public AssemblyAttributeModuleNamesProvider(IHostEnvironment hostingEnvironment)
    {
        var assembly = Assembly.Load(new AssemblyName(hostingEnvironment.ApplicationName));
        _moduleNames = assembly.GetCustomAttributes<ModuleNameAttribute>().Select(m => m.Name).ToList();
    }

    public IEnumerable<string> GetModuleNames()
    {
        return _moduleNames;
    }
}
