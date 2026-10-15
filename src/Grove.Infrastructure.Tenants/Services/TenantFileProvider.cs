using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

namespace Grove.Infrastructure.Tenants.Services;

public class TenantFileProvider : PhysicalFileProvider, ITenantFileProvider
{
    public TenantFileProvider(string root) : base(root)
    {
    }

    public TenantFileProvider(string root, ExclusionFilters filters) : base(root, filters)
    {
    }
}
