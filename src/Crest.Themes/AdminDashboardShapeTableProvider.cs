using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Views;

namespace Crest.Themes;

public sealed class AdminDashboardShapeTableProvider : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("AdminDashboardContent")
            .OnDisplaying(async displaying =>
            {
                await displaying.Shape.AddAsync(new ShapeViewModel("AdminDashboardThemes"), "20");
            });

        return ValueTask.CompletedTask;
    }
}
