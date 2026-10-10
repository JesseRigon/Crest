using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.ContentManagement.Utilities;
using Crest.DisplayManagement.Views;
using Crest.Forms.Models;
using Crest.Forms.ViewModels;
using Crest.Mvc.ModelBinding;

namespace Crest.Forms.Drivers;

public sealed class FormInputElementPartDisplayDriver : ContentPartDisplayDriver<FormInputElementPart>
{
    internal readonly IStringLocalizer S;

    public FormInputElementPartDisplayDriver(IStringLocalizer<FormInputElementPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(FormInputElementPart part, BuildPartEditorContext context)
    {
        return Initialize<FormInputElementPartEditViewModel>("FormInputElementPart_Fields_Edit", m =>
        {
            m.Name = part.Name;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(FormInputElementPart part, UpdatePartEditorContext context)
    {
        var viewModel = new FormInputElementPartEditViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        if (string.IsNullOrWhiteSpace(viewModel.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Name), S["A value is required for Name."]);
        }
        else
        {
            var safeName = viewModel.Name.GetSafeHTMLInputName();

            if (viewModel.Name != safeName)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Name), S["A Name contains invalid characters."]);
            }
        }

        part.Name = viewModel.Name;
        part.ContentItem.DisplayText = part.Name;

        return Edit(part, context);
    }
}
