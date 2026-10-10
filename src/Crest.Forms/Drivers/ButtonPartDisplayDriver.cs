using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Forms.Models;
using Crest.Forms.ViewModels;

namespace Crest.Forms.Drivers;

public sealed class ButtonPartDisplayDriver : ContentPartDisplayDriver<ButtonPart>
{
    public override IDisplayResult Display(ButtonPart part, BuildPartDisplayContext context)
    {
        return View("ButtonPart", part)
            .Location(PlatformConstants.DisplayType.Detail, "Content");
    }

    public override IDisplayResult Edit(ButtonPart part, BuildPartEditorContext context)
    {
        return Initialize<ButtonPartEditViewModel>("ButtonPart_Fields_Edit", m =>
        {
            m.Text = part.Text;
            m.Type = part.Type;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(ButtonPart part, UpdatePartEditorContext context)
    {
        var viewModel = new ButtonPartEditViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        part.Text = viewModel.Text?.Trim();
        part.Type = viewModel.Type?.Trim();

        return Edit(part, context);
    }
}
