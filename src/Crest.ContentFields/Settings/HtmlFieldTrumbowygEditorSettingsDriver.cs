using Acornima;
using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Mvc.ModelBinding;

namespace Crest.ContentFields.Settings;

public sealed class HtmlFieldTrumbowygEditorSettingsDriver : ContentPartFieldDefinitionDisplayDriver<HtmlField>
{
    internal readonly IStringLocalizer S;

    public HtmlFieldTrumbowygEditorSettingsDriver(IStringLocalizer<HtmlFieldTrumbowygEditorSettingsDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ContentPartFieldDefinition partFieldDefinition, BuildEditorContext context)
    {
        return Initialize<TrumbowygSettingsViewModel>("HtmlFieldTrumbowygEditorSettings_Edit", model =>
        {
            var settings = partFieldDefinition.GetSettings<HtmlFieldTrumbowygEditorSettings>();

            model.Options = settings.Options;
            model.InsertMediaWithUrl = settings.InsertMediaWithUrl;
        }).Location("Editor");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition partFieldDefinition, UpdatePartFieldEditorContext context)
    {
        if (partFieldDefinition.Editor() == "Trumbowyg")
        {
            var model = new TrumbowygSettingsViewModel();

            await context.Updater.TryUpdateModelAsync(model, Prefix);

            try
            {
                var options = model.Options.Trim();

                if (!options.StartsWith('{') || !options.EndsWith('}'))
                {
                    throw new Exception();
                }

                var parser = new Parser();

                var optionsScript = parser.ParseScript("var config = " + options);

                var settings = new HtmlFieldTrumbowygEditorSettings
                {
                    InsertMediaWithUrl = model.InsertMediaWithUrl,
                    Options = options,
                };

                context.Builder.WithSettings(settings);
            }
            catch
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Options), S["The options are written in an incorrect format."]);
            }
        }

        return Edit(partFieldDefinition, context);
    }
}
