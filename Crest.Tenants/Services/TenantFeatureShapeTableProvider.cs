using Crest.DisplayManagement;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Views;
using Crest.Modules;
using Crest.Tenants.ViewModels;

namespace Crest.Tenants.Services;

[RequireFeatures("Crest.Features")]
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
