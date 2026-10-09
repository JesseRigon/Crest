using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Forms.Models;
using Crest.Forms.ViewModels;

namespace Crest.Forms.Drivers;

public sealed class ValidationSummaryPartDisplayDriver : ContentPartDisplayDriver<ValidationSummaryPart>
{
    public override IDisplayResult Display(ValidationSummaryPart part, BuildPartDisplayContext context)
    {
        return View("ValidationSummaryPart", part)
            .Location(PlatformConstants.DisplayType.Detail, "Content");
    }

    public override IDisplayResult Edit(ValidationSummaryPart part, BuildPartEditorContext context)
    {
        return Initialize<ValidationSummaryViewModel>("ValidationSummaryPart_Fields_Edit", model =>
        {
            model.ModelOnly = part.ModelOnly;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(ValidationSummaryPart part, UpdatePartEditorContext context)
    {
        var model = new ValidationSummaryViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.ModelOnly = model.ModelOnly;

        return Edit(part, context);
    }
}
