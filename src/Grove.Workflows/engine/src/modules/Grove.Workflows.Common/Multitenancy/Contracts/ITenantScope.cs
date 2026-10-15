using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Common.Multitenancy;

public interface ITenantScope
{
    public IServiceScope ServiceScope { get; }
    IServiceProvider ServiceProvider { get; }
}