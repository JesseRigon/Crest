using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Forms.Models;
using Crest.Forms.ViewModels;

namespace Crest.Forms.Drivers;

public sealed class FormElementValidationPartDisplayDriver : ContentPartDisplayDriver<FormElementValidationPart>
{
    public override IDisplayResult Display(FormElementValidationPart part, BuildPartDisplayContext context)
    {
        return View("FormElementValidationPart", part)
            .Location(PlatformConstants.DisplayType.Detail, "Content:after");
    }

    public override IDisplayResult Edit(FormElementValidationPart part, BuildPartEditorContext context)
    {
        return Initialize<FormElementValidationPartViewModel>("FormElementValidationPart_Fields_Edit", m =>
        {
            m.ValidationOption = part.Option;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(FormElementValidationPart part, UpdatePartEditorContext context)
    {
        var viewModel = new FormElementValidationPartViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        part.Option = viewModel.ValidationOption;

        return Edit(part, context);
    }
}
