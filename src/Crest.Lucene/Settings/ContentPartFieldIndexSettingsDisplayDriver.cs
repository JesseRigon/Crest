using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Lucene.Model;
using Crest.Lucene.ViewModels;

namespace Crest.Lucene.Settings;

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

    public override async Task<IDisplayResult> EditAsync(ContentPartFieldDefinition contentPartFieldDefinition, BuildEditorContext updater)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, LuceneSearchPermissions.ManageLuceneIndexes))
        {
            return null;
        }

        return Initialize<LuceneContentIndexSettingsViewModel>("LuceneContentIndexSettings_Edit", model =>
        {
            model.LuceneContentIndexSettings = contentPartFieldDefinition.GetSettings<LuceneContentIndexSettings>();
        }).Location("Content:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition contentPartFieldDefinition, UpdatePartFieldEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, LuceneSearchPermissions.ManageLuceneIndexes))
        {
            return null;
        }

        var model = new LuceneContentIndexSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(model.LuceneContentIndexSettings);

        return await EditAsync(contentPartFieldDefinition, context);
    }
}
