using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Crest.Workflows.Studio.Workflows.Domain.Contexts;
using Crest.Workflows.Studio.Workflows.Domain.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Providers;

/// <summary>
/// A default implementation of <see cref="IActivityPortProvider"/> that returns the ports defined by the activity descriptor.
/// </summary>
public class DefaultActivityPortProvider : ActivityPortProviderBase
{
    /// <inheritdoc />
    public override double Priority => -1000;

    /// <inheritdoc />
    public override bool GetSupportsActivityType(PortProviderContext context) => true;

    /// <inheritdoc />
    public override IEnumerable<Port> GetPorts(PortProviderContext context)
    {
        return context.ActivityDescriptor.Ports.ToList();
    }
}