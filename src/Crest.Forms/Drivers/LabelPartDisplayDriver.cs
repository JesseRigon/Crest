using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Forms.Models;
using Crest.Forms.ViewModels;

namespace Crest.Forms.Drivers;

public sealed class LabelPartDisplayDriver : ContentPartDisplayDriver<LabelPart>
{
    public override IDisplayResult Display(LabelPart part, BuildPartDisplayContext context)
    {
        return View("LabelPart", part).Location(PlatformConstants.DisplayType.Detail, "Content");
    }

    public override IDisplayResult Edit(LabelPart part, BuildPartEditorContext context)
    {
        return Initialize<LabelPartEditViewModel>("LabelPart_Fields_Edit", m =>
        {
            m.For = part.For;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(LabelPart part, UpdatePartEditorContext context)
    {
        var viewModel = new LabelPartEditViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        part.For = viewModel.For?.Trim();

        return Edit(part, context);
    }
}
