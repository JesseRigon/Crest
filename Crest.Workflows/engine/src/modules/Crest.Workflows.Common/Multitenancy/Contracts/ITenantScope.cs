using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Common.Multitenancy;

public interface ITenantScope
{
    public IServiceScope ServiceScope { get; }
    IServiceProvider ServiceProvider { get; }
}