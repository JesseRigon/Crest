using Crest.DisplayManagement;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Views;
using Crest.Tenants.ViewModels;

namespace Crest.Tenants.Services;

public class TenantShapeTableProvider : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("TenantActionTags")
            .OnDisplaying(async displaying =>
           {
               if (displaying.Shape.TryGetProperty("ShellSettingsEntry", out ShellSettingsEntry entry))
               {
                   await displaying.Shape.AddAsync(new ShapeViewModel<ShellSettingsEntry>("ManageTenantActionTags", entry), "5");
               }
           });

        builder.Describe("TenantActionButtons")
            .OnDisplaying(async displaying =>
            {
                if (displaying.Shape.TryGetProperty("ShellSettingsEntry", out ShellSettingsEntry entry))
                {
                    await displaying.Shape.AddAsync(new ShapeViewModel<ShellSettingsEntry>("ManageTenantActionButtons", entry), "5");
                }
            });

        return ValueTask.CompletedTask;
    }
}
