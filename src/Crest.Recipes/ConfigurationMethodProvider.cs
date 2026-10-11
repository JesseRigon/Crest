using Microsoft.Extensions.Configuration;
using Crest.Environment.Shell.Configuration;
using Crest.Scripting;

namespace Crest.Recipes;

public sealed class ConfigurationMethodProvider : IGlobalMethodProvider
{
    private readonly GlobalMethod _globalMethod;

    public ConfigurationMethodProvider(IShellConfiguration configuration)
    {
        _globalMethod = new GlobalMethod
        {
            Name = "configuration",
            Method = serviceprovider => (Func<string, object, object>)((key, defaultValue) => configuration.GetValue<object>(key, defaultValue)),
        };
    }

    public IEnumerable<GlobalMethod> GetMethods()
    {
        yield return _globalMethod;
    }
}
