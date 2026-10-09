using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Platform.Services;

/// <summary>
/// Temporary replacement for Func until we add proper DI support using DryIoc or Autofac.
/// </summary>
public class Resolver<T>
{
    private readonly IServiceProvider _serviceProvider;
    public Resolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public T Resolve()
    {
        return _serviceProvider.GetService<T>();
    }
}
