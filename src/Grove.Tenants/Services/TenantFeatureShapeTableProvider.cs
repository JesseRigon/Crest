using Grove.DisplayManagement;
using Grove.DisplayManagement.Descriptors;
using Grove.DisplayManagement.Views;
using Grove.Modules;
using Grove.Tenants.ViewModels;

namespace Grove.Tenants.Services;

[RequireFeatures("Grove.Features")]
public class TenantFeatureShapeTableProvider : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("TenantActionButtons")
               .OnDisplaying(async displaying =>
               {
                   if (displaying.Shape.TryGetProperty("ShellSettingsEntry", out ShellSettingsEntry entry))
                   {
                       await displaying.Shape.AddAsync(new ShapeViewModel<ShellSettingsEntry>("ManageFeaturesActionButton", entry), "10");
                   }
               });

        return ValueTask.CompletedTask;
    }
}
