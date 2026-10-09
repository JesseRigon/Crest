using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Views;

namespace Crest.Contents;

public sealed class AdminDashboardShapeTableProvider : ShapeTableProvider
{
    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("AdminDashboardContent")
            .OnDisplaying(async displaying =>
            {
                await displaying.Shape.AddAsync(new ShapeViewModel("AdminDashboardContents"), "10");
            });

        return ValueTask.CompletedTask;
    }
}
