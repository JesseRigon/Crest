using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Crest.Admin;
using Crest.Deployment.Mvc;
using Crest.Deployment.Services;
using Crest.FileStorage;
using Crest.Mvc.Utilities;
using YesSql;

namespace Crest.Deployment.Controllers;

[Admin("DeploymentPlan/ExportFile/{action}/{id?}", "DeploymentPlanExportFile{action}")]
public sealed class ExportFileController : Controller
{
    private readonly IDeploymentManager _deploymentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISession _session;
    private readonly ITempDirectoryProvider _tempDirectoryProvider;

    public ExportFileController(
        IAuthorizationService authorizationService,
        ISession session,
        IDeploymentManager deploymentManager,
        ITempDirectoryProvider tempDirectoryProvider)
    {
        _authorizationService = authorizationService;
        _deploymentManager = deploymentManager;
        _session = session;
        _tempDirectoryProvider = tempDirectoryProvider;
    }

    [HttpPost]
    [DeleteFileResultFilter]
    public async Task<IActionResult> Execute(long id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DeploymentPermissions.Export))
        {
            return Forbid();
        }

        var deploymentPlan = await _session.GetAsync<DeploymentPlan>(id);

        if (deploymentPlan == null)
        {
            return NotFound();
        }

        var filename = deploymentPlan.Name.ToSafeName() + ".zip";
        var archiveFileName = await DeploymentPackages.CreateArchiveAsync(_deploymentManager, _tempDirectoryProvider, deploymentPlan);

        return new PhysicalFileResult(archiveFileName, MediaTypeNames.Application.Zip) { FileDownloadName = filename };
    }
}
