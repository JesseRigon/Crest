using Grove.Contents.ViewModels;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Lists.Models;
using Grove.Lists.ViewModels;

namespace Grove.Lists.Drivers;

public sealed class ListPartContentsAdminListDisplayDriver : DisplayDriver<ContentOptionsViewModel>
{
    protected override void BuildPrefix(ContentOptionsViewModel model, string htmlFieldPrefix)
    {
        Prefix = "ListPart";
    }

    public override IDisplayResult Edit(ContentOptionsViewModel model, BuildEditorContext context)
    {
        return Dynamic("ContentsAdminList__ListPartFilter").Location("Actions:20");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentOptionsViewModel model, UpdateEditorContext context)
    {
        var viewModel = new ListPartContentsAdminFilterViewModel();
        await context.Updater.TryUpdateModelAsync(viewModel, nameof(ListPart));

        if (viewModel.ShowListContentTypes)
        {
            model.RouteValues.TryAdd("ListPart.ShowListContentTypes", viewModel.ShowListContentTypes);
        }

        if (!string.IsNullOrEmpty(viewModel.ListContentItemId))
        {
            model.RouteValues.TryAdd("ListPart.ListContentItemId", viewModel.ListContentItemId);
        }

        return Edit(model, context);
    }
}
