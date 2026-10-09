using System.Text.Json;
using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Mvc.ModelBinding;
using Crest.Mvc.Utilities;

namespace Crest.ContentFields.Settings;

public sealed class TextFieldMonacoEditorSettingsDriver : ContentPartFieldDefinitionDisplayDriver<TextField>
{
    internal readonly IStringLocalizer S;

    public TextFieldMonacoEditorSettingsDriver(IStringLocalizer<TextFieldMonacoEditorSettingsDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<MonacoSettingsViewModel>("TextFieldMonacoEditorSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<TextFieldMonacoEditorSettings>();
            if (string.IsNullOrWhiteSpace(settings.Options))
            {
                settings.Options = JConvert.SerializeObject(new { automaticLayout = true, language = "html" }, JOptions.Indented);
            }

            model.Options = settings.Options;
        }).Location("Editor");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        if (partFieldDefinition.Editor() == "Monaco")
        {
            var model = new MonacoSettingsViewModel();

            await context.Updater.TryUpdateModelAsync(model, Prefix);

            if (!model.Options.IsJson())
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Options), S["The options are written in an incorrect format."]);
            }
            else
            {
                var settings = new TextFieldMonacoEditorSettings
                {
                    Options = model.Options,
                };
                context.Builder.WithSettings(settings);
            }
        }

        return Edit(partFieldDefinition, context);
    }
}
