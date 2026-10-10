using Crest.Mvc.ModelBinding;
using Crest.Tenants.Models;

namespace Crest.Tenants.Services;

public interface ITenantValidator
{
    Task<IEnumerable<ModelError>> ValidateAsync(TenantModelBase model);
}
