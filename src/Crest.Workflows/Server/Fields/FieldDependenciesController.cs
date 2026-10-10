using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Workflows.Fields;

/// <summary>A definition's field dependencies resolved against the tenant's definitions now - the "what this flow needs" panel. Reading takes View.</summary>
[ApiController, AutoValidateAntiforgeryToken, Route(WorkflowsConstants.Routes.DefinitionsApi)]
public sealed class FieldDependenciesController(WorkflowFieldDependencyAnalyzer analyzer, IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet(WorkflowsConstants.Routes.DefinitionFieldDependenciesApi)]
    public async Task<IActionResult> GetAsync(string definitionId, CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ViewWorkflows))
        {
            return Forbid();
        }

        var report = await analyzer.AnalyzeAsync(definitionId, cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }
}
