using System.Text.Json;
using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Forms.Models;
using Crest.Forms.ViewModels;

namespace Crest.Forms.Drivers;

public sealed class SelectPartDisplayDriver : ContentPartDisplayDriver<SelectPart>
{
    internal readonly IStringLocalizer S;

    public SelectPartDisplayDriver(IStringLocalizer<SelectPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Display(SelectPart part, BuildPartDisplayContext context)
    {
        return View("SelectPart", part).Location(PlatformConstants.DisplayType.Detail, "Content");
    }

    public override IDisplayResult Edit(SelectPart part, BuildPartEditorContext context)
    {
        return Initialize<SelectPartEditViewModel>("SelectPart_Fields_Edit", m =>
        {
            m.Options = JConvert.SerializeObject(part.Options ?? [], JOptions.CamelCaseIndented);
            m.DefaultValue = part.DefaultValue;
            m.Editor = part.Editor;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(SelectPart part, UpdatePartEditorContext context)
    {
        var viewModel = new SelectPartEditViewModel();
        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);
        part.DefaultValue = viewModel.DefaultValue;

        try
        {
            part.Editor = viewModel.Editor;
            part.Options = string.IsNullOrWhiteSpace(viewModel.Options)
                ? []
                : JConvert.DeserializeObject<SelectOption[]>(viewModel.Options);
        }
        catch
        {
            context.Updater.ModelState.AddModelError(Prefix + '.' + nameof(SelectPartEditViewModel.Options), S["The options are written in an incorrect format."]);
        }

        return Edit(part, context);
    }
}
