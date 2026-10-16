using Grove.Communications.Delivery;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Modules;
using Grove.Security.Permissions;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Communications.Delivery;

[Feature(CommunicationsConstants.ScreensFeatureId)]
public sealed class DeliveryLogStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IDeliveryLog, YesSqlDeliveryLog>();
        services.AddIndexProvider<DeliveryRecordIndexProvider>();
        services.AddDataMigration<DeliveryMigrations>();
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(CommunicationsConstants.DeliveryCollection));
        services.AddPermissionProvider<DeliveryLogPermissions>();
        services.AddApiContributor<DeliveryLogApi>();
        services.AddPageSource<CommunicationsPages>();
    }
}

public sealed class DeliveryLogPermissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _all = [CommunicationsPermissions.ViewDeliveryLog];

    public Task<IEnumerable<Permission>> GetPermissionsAsync() => Task.FromResult(_all);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype { Name = PlatformConstants.Roles.Administrator, Permissions = _all },
    ];
}
