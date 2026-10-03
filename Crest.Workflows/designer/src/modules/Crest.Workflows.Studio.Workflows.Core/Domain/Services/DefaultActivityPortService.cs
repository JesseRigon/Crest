using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Crest.Workflows.Studio.Workflows.Domain.Contexts;
using Crest.Workflows.Studio.Workflows.Domain.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Services;

/// <inheritdoc />
public class DefaultActivityPortService : IActivityPortService
{
    private readonly IEnumerable<IActivityPortProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultActivityPortService"/> class.
    /// </summary>
    public DefaultActivityPortService(IEnumerable<IActivityPortProvider> providers)
    {
        _providers = providers;
    }

    /// <inheritdoc />
    public IActivityPortProvider GetProvider(PortProviderContext context)
    {
        var activityType = context.ActivityDescriptor.TypeName;
        var provider = _providers.Where(x => x.GetSupportsActivityType(context)).MaxBy(x => x.Priority);
        return provider ?? throw new Exception($"No port provider found for activity type '{activityType}'.");
    }

    /// <inheritdoc />
    public IEnumerable<Port> GetPorts(PortProviderContext context)
    {
        var provider = GetProvider(context);
        return provider.GetPorts(context);
    }
}