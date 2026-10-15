using Grove.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Grove.Workflows.Studio.Workflows.Domain.Contexts;

namespace Grove.Workflows.Studio.Workflows.Domain.Contracts;

/// <summary>
/// Defines the contract for activity port service.
/// </summary>
public interface IActivityPortService
{
    IActivityPortProvider GetProvider(PortProviderContext context);
    IEnumerable<Port> GetPorts(PortProviderContext context);
}