using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Elasticsearch.Core.Models;
using Crest.Elasticsearch.ViewModels;

namespace Crest.Elasticsearch.Drivers;

public sealed class ContentPartFieldIndexSettingsDisplayDriver : ContentPartFieldDefinitionDisplayDriver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public ContentPartFieldIndexSettingsDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override async Task<IDisplayResult> EditAsync(ContentPartFieldDefinition contentPartFieldDefinition, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, ElasticsearchPermissions.ManageElasticIndexes))
        {
            return null;
        }

        return Initialize<ElasticContentIndexSettingsViewModel>("ElasticContentIndexSettings_Edit", model =>
        {
            model.ElasticContentIndexSettings = contentPartFieldDefinition.GetSettings<ElasticContentIndexSettings>();
        }).Location("Content:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition contentPartFieldDefinition, UpdatePartFieldEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, ElasticsearchPermissions.ManageElasticIndexes))
        {
            return null;
        }

        var model = new ElasticContentIndexSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(model.ElasticContentIndexSettings);

        return await EditAsync(contentPartFieldDefinition, context);
    }
}
