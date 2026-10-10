using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Crest.Admin;
using Crest.Media.Azure.ViewModels;
using Crest.Modules;

namespace Crest.Media.Azure;

[Feature("Crest.Media.Azure.Storage")]
[Admin("MediaAzureBlob/{action}", "AzureBlob.{action}")]
public sealed class AdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly MediaBlobStorageOptions _options;

    public AdminController(
        IAuthorizationService authorizationService,
        IOptions<MediaBlobStorageOptions> options)
    {
        _authorizationService = authorizationService;
        _options = options.Value;
    }

    public async Task<IActionResult> Options()
    {
        if (!await _authorizationService.AuthorizeAsync(User, Permissions.ViewAzureMediaOptions))
        {
            return Forbid();
        }

        var model = new OptionsViewModel
        {
            CreateContainer = _options.CreateContainer,
            RemoveContainer = _options.RemoveContainer,
            RemoveFilesFromBasePath = _options.RemoveFilesFromBasePath,
            ContainerName = _options.ContainerName,
            ConnectionString = _options.ConnectionString,
            BasePath = _options.BasePath,
        };

        return View(model);
    }
}
