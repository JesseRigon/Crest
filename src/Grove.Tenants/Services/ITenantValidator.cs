using Grove.Mvc.ModelBinding;
using Grove.Tenants.Models;

namespace Grove.Tenants.Services;

public interface ITenantValidator
{
    Task<IEnumerable<ModelError>> ValidateAsync(TenantModelBase model);
}
