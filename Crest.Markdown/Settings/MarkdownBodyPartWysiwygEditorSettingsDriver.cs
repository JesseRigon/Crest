using Acornima;
using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Markdown.Models;
using Crest.Markdown.ViewModels;
using Crest.Mvc.ModelBinding;

namespace Crest.Markdown.Settings;

public sealed class MarkdownBodyPartWysiwygEditorSettingsDriver : ContentTypePartDefinitionDisplayDriver<MarkdownBodyPart>
{
    internal readonly IStringLocalizer S;

    public MarkdownBodyPartWysiwygEditorSettingsDriver(IStringLocalizer<MarkdownBodyPartWysiwygEditorSettingsDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<MarkdownFieldWysiwygEditorSettingsViewModel>("MarkdownBodyPartWysiwygEditorSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<MarkdownBodyPartWysiwygEditorSettings>();

            model.Options = settings.Options;
        }).Location("Editor");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        if (contentTypePartDefinition.Editor() == "Wysiwyg")
        {
            var model = new MarkdownFieldWysiwygEditorSettingsViewModel();

            await context.Updater.TryUpdateModelAsync(model, Prefix);

            if (!string.IsNullOrWhiteSpace(model.Options))
            {
                var options = model.Options.Trim();

                if (!options.StartsWith('{') || !options.EndsWith('}'))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Options), S["The options are written in an incorrect format."]);
                }
                else
                {
                    try
                    {
                        var parser = new Parser();

                        parser.ParseScript("var config = " + options);

                        var settings = new MarkdownBodyPartWysiwygEditorSettings
                        {
                            Options = options,
                        };

                        context.Builder.WithSettings(settings);
                    }
                    catch (ParseErrorException)
                    {
                        context.Updater.ModelState.AddModelError(Prefix, nameof(model.Options), S["The options are written in an incorrect format."]);
                    }
                }
            }
            else
            {
                context.Builder.WithSettings(new MarkdownBodyPartWysiwygEditorSettings());
            }
        }

        return Edit(contentTypePartDefinition, context);
    }
}
