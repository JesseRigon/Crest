using System.Text.Json;
using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.ContentFields.Settings;

public sealed class MultiTextFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<MultiTextField>
{
    internal readonly IStringLocalizer S;

    public MultiTextFieldSettingsDriver(IStringLocalizer<MultiTextFieldSettingsDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<MultiTextFieldSettingsViewModel>("MultiTextFieldSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<MultiTextFieldSettings>();

            model.Required = settings.Required;
            model.Hint = settings.Hint;
            model.Options = JConvert.SerializeObject(settings.Options, JOptions.Indented);
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        var model = new MultiTextFieldSettingsViewModel();
        var settings = new MultiTextFieldSettings();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.Required = model.Required;
        settings.Hint = model.Hint;

        try
        {
            settings.Options = JConvert.DeserializeObject<MultiTextFieldValueOption[]>(model.Options);

            context.Builder.WithSettings(settings);
        }
        catch
        {
            context.Updater.ModelState.AddModelError(Prefix, S["The options are written in an incorrect format."]);
        }

        return Edit(partFieldDefinition, context);
    }
}
