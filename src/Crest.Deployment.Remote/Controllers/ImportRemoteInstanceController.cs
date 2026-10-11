using System.IO.Compression;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Crest.Deployment.Remote.Services;
using Crest.Deployment.Remote.ViewModels;
using Crest.Deployment.Services;
using Crest.DisplayManagement.Notify;
using Crest.FileStorage;
using Crest.Recipes.Models;

namespace Crest.Deployment.Remote.Controllers;

public sealed class ImportRemoteInstanceController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IDeploymentManager _deploymentManager;
    private readonly INotifier _notifier;
    private readonly ILogger _logger;
    private readonly FileCreationService _fileCreationService;
    private readonly ITempDirectoryProvider _tempDirectoryProvider;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public ImportRemoteInstanceController(
        IAuthorizationService authorizationService,
        IDeploymentManager deploymentManager,
        FileCreationService fileCreationService,
        ITempDirectoryProvider tempDirectoryProvider,
        INotifier notifier,
        IHtmlLocalizer<ImportRemoteInstanceController> htmlLocalizer,
        IStringLocalizer<ImportRemoteInstanceController> stringLocalizer,
        ILogger<ImportRemoteInstanceController> logger)
    {
        _deploymentManager = deploymentManager;
        _fileCreationService = fileCreationService;
        _tempDirectoryProvider = tempDirectoryProvider;
        _notifier = notifier;
        _logger = logger;
        _authorizationService = authorizationService;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <remarks>
    /// A bearer call from another instance (the remote deployment key, authenticated by the
    /// request path's gate through the Api forwarder), so antiforgery does not apply; the
    /// import runs as the remote client's caller, which holds exactly
    /// <see cref="DeploymentPermissions.ImportRemoteInstances"/>.
    /// </remarks>
    [HttpPost]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Import(ImportViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DeploymentPermissions.ImportRemoteInstances))
        {
            return Forbid();
        }

        if (model.Content is null)
        {
            return StatusCode((int)HttpStatusCode.BadRequest, "No package was provided");
        }

        // Create a temporary filename to save the archive
        var tempArchiveName = _tempDirectoryProvider.GetTempFileName(".zip");

        // Create a temporary folder to extract the archive to
        var tempArchiveFolder = _tempDirectoryProvider.GetTempFileName();

        try
        {
            await using var uploadedStream = model.Content.OpenReadStream();
            await using var fileCreatingResult = await _fileCreationService.CreateAsync(
                new FileCreatingContext(model.Content.FileName, model.Content.Length, model.Content.ContentType),
                uploadedStream,
                HttpContext.RequestAborted);

            if (!fileCreatingResult.Succeeded)
            {
                return StatusCode((int)HttpStatusCode.BadRequest, fileCreatingResult.ErrorMessage ?? $"The uploaded file '{model.Content.FileName}' was rejected.");
            }

            await using (var fs = System.IO.File.Create(tempArchiveName))
            {
                await fileCreatingResult.Stream.CopyToAsync(fs, HttpContext.RequestAborted);
            }

            ZipFile.ExtractToDirectory(tempArchiveName, tempArchiveFolder);

            await _deploymentManager.ImportDeploymentPackageAsync(new PhysicalFileProvider(tempArchiveFolder));
        }
        catch (RecipeExecutionException e)
        {
            _logger.LogError(e, "Unable to import a recipe from deployment plan.");

            await _notifier.ErrorAsync(H["The deployment plan failed with the following errors: {0}", string.Join(' ', e.StepResult.Errors)]);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected error occurred while executing a deployment plan.");

            await _notifier.ErrorAsync(H["Unexpected error occurred while executing a deployment plan."]);
        }
        finally
        {
            if (System.IO.File.Exists(tempArchiveName))
            {
                System.IO.File.Delete(tempArchiveName);
            }

            if (Directory.Exists(tempArchiveFolder))
            {
                Directory.Delete(tempArchiveFolder, true);
            }
        }

        return Ok();
    }
}
