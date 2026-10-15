using Grove.Workflows.Common.Multitenancy;
using Grove.Workflows.Tenants.Providers;

namespace Grove.Workflows.Tenants.Options;

/// <summary>
/// Options for configuring the Tenants.
/// </summary>
public class TenantsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether multitenancy is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the tenants through configuration. Will be used by the <see cref="ConfigurationTenantsProvider"/>
    /// </summary>
    public ICollection<Tenant> Tenants { get; set; } = new List<Tenant>();
}