using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Forms.Models;
using Crest.Forms.ViewModels;
using Crest.Mvc.ModelBinding;

namespace Crest.Forms.Drivers;

public sealed class TextAreaPartDisplayDriver : ContentPartDisplayDriver<TextAreaPart>
{
    private readonly IStringLocalizer S;

    public TextAreaPartDisplayDriver(IStringLocalizer<TextAreaPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Display(TextAreaPart part, BuildPartDisplayContext context)
    {
        return View("TextAreaPart", part).Location(PlatformConstants.DisplayType.Detail, "Content");
    }

    public override IDisplayResult Edit(TextAreaPart part, BuildPartEditorContext context)
    {
        return Initialize<TextAreaPartEditViewModel>("TextAreaPart_Fields_Edit", m =>
        {
            m.Placeholder = part.Placeholder;
            m.DefaultValue = part.DefaultValue;
            m.Rows = part.Rows;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(TextAreaPart part, UpdatePartEditorContext context)
    {
        var viewModel = new TextAreaPartEditViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        part.Placeholder = viewModel.Placeholder?.Trim();
        part.DefaultValue = viewModel.DefaultValue?.Trim();
        part.Rows = viewModel.Rows;

        if (viewModel.Rows < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Rows), S["The Rows field should be greater than or equal 1."]);
        }

        return Edit(part, context);
    }
}
