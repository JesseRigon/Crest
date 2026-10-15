using System.IO.Compression;
using Grove.Api;
using Grove.Data.Storage;
using Grove.Deployment.Remote.Services;
using Grove.Deployment.Remote.ViewModels;
using Grove.Deployment.Services;
using Grove.Infrastructure.Recipes.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Grove.Deployment.Remote.Api;

/// <summary>
/// The remote deployment import (docs/MVC/api.md › P11): <c>POST
/// Grove.Deployment.Remote/ImportRemoteInstance/Import</c>, the address an exporting instance's
/// remote instance URL names. A machine call: the remote deployment key in the
/// <c>Authorization</c> header is authenticated by the gate through the Api forwarder
/// (<see cref="RemoteDeploymentKeyAuthenticationHandler"/>), so antiforgery does not apply; the
/// import runs as the remote client's caller, which holds exactly
/// <see cref="DeploymentPermissions.ImportRemoteInstances"/>. The package is the multipart form's
/// <c>Content</c> file (<see cref="ImportViewModel"/>).
/// </summary>
public sealed class RemoteImportApi : ApiContributor
{
    public const string Path = "Grove.Deployment.Remote/ImportRemoteInstance/Import";

    public override string Name => "RemoteImport";

    public override string FeatureId => "Grove.Deployment.Remote";

    public override string RoutePrefix => "";

    public override void Map(IEndpointRouteBuilder group)
    {
        group.MapPost(Path, (HttpContext http, [FromForm] ImportViewModel model) => ApiHandlers.For<RemoteImportHandlers>(http).ImportAsync(model))
            .RequireAuthorization()
            .SkipApiAntiforgery()
            .DisableAntiforgery()
            .ExcludeFromDescription();
    }
}

public sealed class RemoteImportHandlers(
    IAuthorizationService authorizationService,
    IDeploymentManager deploymentManager,
    FileCreationService fileCreationService,
    ITempDirectoryProvider tempDirectoryProvider,
    ILogger<RemoteImportHandlers> logger) : ApiHandlers
{
    public async Task<IResult> ImportAsync(ImportViewModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, DeploymentPermissions.ImportRemoteInstances))
        {
            return Forbid();
        }

        if (model?.Content is null)
        {
            return ApiResults.BadRequest("No package was provided");
        }

        // A temporary file to save the archive to, and a temporary folder to extract it to.
        var tempArchiveName = tempDirectoryProvider.GetTempFileName(".zip");
        var tempArchiveFolder = tempDirectoryProvider.GetTempFileName();

        try
        {
            await using var uploadedStream = model.Content.OpenReadStream();
            await using var fileCreatingResult = await fileCreationService.CreateAsync(
                new FileCreatingContext(model.Content.FileName, model.Content.Length, model.Content.ContentType),
                uploadedStream,
                HttpContext.RequestAborted);

            if (!fileCreatingResult.Succeeded)
            {
                return ApiResults.BadRequest(fileCreatingResult.ErrorMessage ?? $"The uploaded file '{model.Content.FileName}' was rejected.");
            }

            await using (var fs = System.IO.File.Create(tempArchiveName))
            {
                await fileCreatingResult.Stream.CopyToAsync(fs, HttpContext.RequestAborted);
            }

            ZipFile.ExtractToDirectory(tempArchiveName, tempArchiveFolder);

            await deploymentManager.ImportDeploymentPackageAsync(new PhysicalFileProvider(tempArchiveFolder));
        }
        catch (RecipeExecutionException e)
        {
            logger.LogError(e, "Unable to import a recipe from deployment plan.");

            return ApiProblems.BadRequest($"The deployment plan failed with the following errors: {string.Join(' ', e.StepResult.Errors)}");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unexpected error occurred while executing a deployment plan.");

            return Results.Problem("Unexpected error occurred while executing a deployment plan.", statusCode: StatusCodes.Status500InternalServerError);
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
