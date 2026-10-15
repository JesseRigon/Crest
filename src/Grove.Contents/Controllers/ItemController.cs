using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grove.ContentManagement;
using Grove.ContentManagement.Display;
using Grove.DisplayManagement.ModelBinding;

namespace Grove.Contents.Controllers;

public sealed class ItemController : Controller, IUpdateModel
{
    private readonly IContentManager _contentManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IAuthorizationService _authorizationService;

    public ItemController(
        IContentManager contentManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IAuthorizationService authorizationService)
    {
        _contentManager = contentManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _authorizationService = authorizationService;
    }

    // Display is the site shell's content route now (docs/blazor-display.md rework step 3); the
    // DisplayContentItem route only generates its URLs.

    public async Task<IActionResult> Preview(string contentItemId)
    {
        if (contentItemId == null)
        {
            return NotFound();
        }

        var versionOptions = VersionOptions.Latest;

        var contentItem = await _contentManager.GetAsync(contentItemId, versionOptions);

        if (contentItem == null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.PreviewContent, contentItem))
        {
            return this.ChallengeOrForbid();
        }

        var model = await _contentItemDisplayManager.BuildDisplayAsync(contentItem, this);

        return View(model);
    }
}
