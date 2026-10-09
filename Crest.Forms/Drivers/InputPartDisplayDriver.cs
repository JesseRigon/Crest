using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Forms.Models;
using Crest.Forms.ViewModels;

namespace Crest.Forms.Drivers;

public sealed class InputPartDisplayDriver : ContentPartDisplayDriver<InputPart>
{
    public override IDisplayResult Display(InputPart part, BuildPartDisplayContext context)
    {
        return View("InputPart", part).Location(PlatformConstants.DisplayType.Detail, "Content");
    }

    public override IDisplayResult Edit(InputPart part, BuildPartEditorContext context)
    {
        return Initialize<InputPartEditViewModel>("InputPart_Fields_Edit", m =>
        {
            m.Placeholder = part.Placeholder;
            m.DefaultValue = part.DefaultValue;
            m.Type = part.Type;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(InputPart part, UpdatePartEditorContext context)
    {
        var viewModel = new InputPartEditViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        part.Placeholder = viewModel.Placeholder?.Trim();
        part.DefaultValue = viewModel.DefaultValue?.Trim();
        part.Type = viewModel.Type?.Trim();

        return Edit(part, context);
    }
}
