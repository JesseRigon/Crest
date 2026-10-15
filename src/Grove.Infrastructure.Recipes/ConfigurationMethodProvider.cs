using Microsoft.Extensions.Configuration;
using Grove.Environment.Shell.Configuration;
using Grove.Scripting;

namespace Grove.Infrastructure.Recipes;

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
