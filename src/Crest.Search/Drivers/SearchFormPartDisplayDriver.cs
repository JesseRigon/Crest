using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Search.Models;
using Crest.Search.ViewModels;

namespace Crest.Search.Drivers;

public sealed class SearchFormPartDisplayDriver : ContentPartDisplayDriver<SearchFormPart>
{
    public override IDisplayResult Display(SearchFormPart part, BuildPartDisplayContext context)
    {
        return View(GetDisplayShapeType(context), part)
            .Location(PlatformConstants.DisplayType.Detail, "Content");
    }

    public override IDisplayResult Edit(SearchFormPart part, BuildPartEditorContext context)
    {
        return Initialize<SearchPartViewModel>(GetEditorShapeType(context), viewModel =>
        {
            viewModel.Placeholder = part.Placeholder;
            viewModel.IndexName = part.IndexName;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(SearchFormPart part, UpdatePartEditorContext context)
    {
        var model = new SearchPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Placeholder = model.Placeholder;
        part.IndexName = string.IsNullOrWhiteSpace(model.IndexName) ? null : model.IndexName.Trim();

        return Edit(part, context);
    }
}
