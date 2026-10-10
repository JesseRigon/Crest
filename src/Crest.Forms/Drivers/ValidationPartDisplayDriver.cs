using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Forms.Models;
using Crest.Forms.ViewModels;

namespace Crest.Forms.Drivers;

public sealed class ValidationPartDisplayDriver : ContentPartDisplayDriver<ValidationPart>
{
    public override IDisplayResult Display(ValidationPart part, BuildPartDisplayContext context)
    {
        return View("ValidationPart", part).Location(PlatformConstants.DisplayType.Detail, "Content");
    }

    public override IDisplayResult Edit(ValidationPart part, BuildPartEditorContext context)
    {
        return Initialize<ValidationPartEditViewModel>("ValidationPart_Fields_Edit", m =>
        {
            m.For = part.For;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(ValidationPart part, UpdatePartEditorContext context)
    {
        var viewModel = new ValidationPartEditViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        part.For = viewModel.For?.Trim();

        return Edit(part, context);
    }
}
