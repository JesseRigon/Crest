using Microsoft.Extensions.Localization;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.ViewModels;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.ContentTypes.Editors;

public sealed class DefaultContentTypeDisplayDriver : ContentTypeDefinitionDisplayDriver
{
    internal readonly IStringLocalizer S;

    public DefaultContentTypeDisplayDriver(IStringLocalizer<DefaultContentTypeDisplayDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Edit(ContentTypeDefinition contentTypeDefinition, BuildEditorContext context)
    {
        return Initialize<ContentTypeViewModel>("ContentType_Edit", model =>
        {
            model.DisplayName = contentTypeDefinition.DisplayName;
            model.Name = contentTypeDefinition.Name;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypeDefinition contentTypeDefinition, UpdateTypeEditorContext context)
    {
        var model = new ContentTypeViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithDisplayName(model.DisplayName);

        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            context.Updater.ModelState.AddModelError("DisplayName", S["The Content Type name can't be empty."]);
        }

        return Edit(contentTypeDefinition, context);
    }
}
