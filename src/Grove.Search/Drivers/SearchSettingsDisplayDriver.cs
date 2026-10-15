using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Grove.DisplayManagement.Entities;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Indexing;
using Grove.Search.Models;
using Grove.Search.ViewModels;
using Grove.Settings;

namespace Grove.Search.Drivers;

public sealed class SearchSettingsDisplayDriver : SiteDisplayDriver<SearchSettings>
{
    [Obsolete("This property should not be used. Instead use  SearchConstants.SearchSettingsGroupId.")]
    public const string GroupId = SearchConstants.SearchSettingsGroupId;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IIndexProfileStore _indexProfileStore;

    public SearchSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IIndexProfileStore indexProfileStore
        )
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _indexProfileStore = indexProfileStore;
    }

    protected override string SettingsGroupId
        => SearchConstants.SearchSettingsGroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, SearchSettings settings, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, SearchPermissions.ManageSearchSettings))
        {
            return null;
        }

        return Initialize<SearchSettingsViewModel>("SearchSettings_Edit", async model =>
        {
            model.DefaultIndexProfileName = settings.DefaultIndexProfileName;
            model.Placeholder = settings.Placeholder;
            model.PageTitle = settings.PageTitle;
            model.Indexes = (await _indexProfileStore.GetAllAsync())
                .Select(index => new SelectListItem(index.Name, index.Name))
                .ToArray();
        }).Location("Content:2")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, SearchSettings section, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, SearchPermissions.ManageSearchSettings))
        {
            return null;
        }

        var model = new SearchSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        section.DefaultIndexProfileName = model.DefaultIndexProfileName;
        section.Placeholder = model.Placeholder;
        section.PageTitle = model.PageTitle;

        return await EditAsync(site, section, context);
    }
}
