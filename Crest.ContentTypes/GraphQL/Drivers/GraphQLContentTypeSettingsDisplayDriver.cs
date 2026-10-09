using Microsoft.Extensions.Options;
using Crest.ContentManagement.GraphQL.Options;
using Crest.ContentManagement.GraphQL.Settings;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.ContentTypes.GraphQL.ViewModels;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.ContentTypes.GraphQL.Drivers;

public sealed class GraphQLContentTypeSettingsDisplayDriver : ContentTypeDefinitionDisplayDriver
{
    private readonly GraphQLContentOptions _contentOptions;

    public GraphQLContentTypeSettingsDisplayDriver(IOptions<GraphQLContentOptions> optionsAccessor)
    {
        _contentOptions = optionsAccessor.Value;
    }

    public override IDisplayResult Edit(ContentTypeDefinition contentTypeDefinition, BuildEditorContext context)
    {
        return Initialize<GraphQLContentTypeSettingsViewModel>("GraphQLContentTypeSettings_Edit", model =>
        {
            model.Definition = contentTypeDefinition;
            model.Settings = contentTypeDefinition.GetSettings<GraphQLContentTypeSettings>();
            model.Options = _contentOptions;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypeDefinition contentTypeDefinition, UpdateTypeEditorContext context)
    {
        var model = new GraphQLContentTypeSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(model.Settings);

        return Edit(contentTypeDefinition, context);
    }
}
