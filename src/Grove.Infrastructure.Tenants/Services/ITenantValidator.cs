using Grove.Infrastructure.Tenants.Models;

namespace Grove.Infrastructure.Tenants.Services;

public interface ITenantValidator
{
    Task<IEnumerable<TenantError>> ValidateAsync(TenantModelBase model);
}
