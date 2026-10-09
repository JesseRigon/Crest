using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Forms.Models;
using Crest.Forms.ViewModels;

namespace Crest.Forms.Drivers;

public sealed class FormElementPartDisplayDriver : ContentPartDisplayDriver<FormElementPart>
{
    public override IDisplayResult Edit(FormElementPart part, BuildPartEditorContext context)
    {
        return Initialize<FormElementPartEditViewModel>("FormElementPart_Fields_Edit", m =>
        {
            m.Id = part.Id;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(FormElementPart part, UpdatePartEditorContext context)
    {
        var viewModel = new FormElementPartEditViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        part.Id = viewModel.Id?.Trim();

        return Edit(part, context);
    }
}
