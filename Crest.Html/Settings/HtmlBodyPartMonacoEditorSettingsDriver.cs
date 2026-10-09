using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Html.Models;
using Crest.Html.ViewModels;
using Crest.Mvc.ModelBinding;
using Crest.Mvc.Utilities;

namespace Crest.Html.Settings;

public sealed class HtmlBodyPartMonacoEditorSettingsDriver : ContentTypePartDefinitionDisplayDriver<HtmlBodyPart>
{
    internal readonly IStringLocalizer S;

    public HtmlBodyPartMonacoEditorSettingsDriver(IStringLocalizer<HtmlBodyPartMonacoEditorSettingsDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<MonacoSettingsViewModel>("HtmlBodyPartMonacoSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<HtmlBodyPartMonacoEditorSettings>();
            if (string.IsNullOrWhiteSpace(settings.Options))
            {
                settings.Options = JConvert.SerializeObject(new { automaticLayout = true, language = "html" }, JOptions.Indented);
            }
            model.Options = settings.Options;
        }).Location("Editor");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        if (contentTypePartDefinition.Editor() == "Monaco")
        {
            var model = new MonacoSettingsViewModel();

            await context.Updater.TryUpdateModelAsync(model, Prefix);

            if (!model.Options.IsJson())
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Options), S["The options are written in an incorrect format."]);
            }
            else
            {
                var jsonSettings = JObject.Parse(model.Options);
                jsonSettings["language"] = "html";
                var settings = new HtmlBodyPartMonacoEditorSettings
                {
                    Options = jsonSettings.ToString(),
                };
                context.Builder.WithSettings(settings);
            }
        }

        return Edit(contentTypePartDefinition, context);
    }
}
