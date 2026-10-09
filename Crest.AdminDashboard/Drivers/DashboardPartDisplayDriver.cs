using Crest.AdminDashboard.Models;
using Crest.AdminDashboard.ViewModels;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;

namespace Crest.AdminDashboard.Drivers;

public sealed class DashboardPartDisplayDriver : ContentPartDisplayDriver<DashboardPart>
{
    public override Task<IDisplayResult> DisplayAsync(DashboardPart part, BuildPartDisplayContext context)
    {
        return Task.FromResult<IDisplayResult>(null);
    }

    public override IDisplayResult Edit(DashboardPart dashboardPart, BuildPartEditorContext context)
    {
        return Initialize<DashboardPartViewModel>(GetEditorShapeType(context), m => BuildViewModel(m, dashboardPart));
    }

    public override async Task<IDisplayResult> UpdateAsync(DashboardPart model, UpdatePartEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix,
            t => t.Position,
            t => t.Width,
            t => t.Height);

        return Edit(model, context);
    }

    private static void BuildViewModel(DashboardPartViewModel model, DashboardPart part)
    {
        model.Position = part.Position;
        model.Width = part.Width;
        model.Height = part.Height;
        model.DashboardPart = part;
        model.ContentItem = part.ContentItem;
    }
}
