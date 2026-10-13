using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Crest.Services;
using Crest.Indexing;
using Crest.Indexing;
using Crest.Indexing.Models;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController, AutoValidateAntiforgeryToken, Route("api/crest/indexes")]
public sealed class IndexesController(IRequestAccess requestAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IndexSummary[]>> ListAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, IndexingPermissions.ManageIndexes);
        if (access is null) return Forbid();
        var profiles = access.GetRequiredService<IIndexProfileStore>();
        var items = await profiles.GetAllAsync();
        return Ok(items.OrderBy(profile => profile.Name).Select(IndexSummary.From).ToArray());
    }

    [HttpPost("{id}/rebuild")]
    public async Task<ActionResult<IndexSummary>> RebuildAsync(string id)
    {
        var access = await requestAccess.AuthorizeAsync(User, IndexingPermissions.ManageIndexes);
        if (access is null) return Forbid();
        var profiles = access.GetRequiredService<IIndexProfileStore>();
        var profile = await profiles.FindByIdAsync(id);
        if (profile is null) return NotFound();
        var manager = access.GetRequiredService<IServiceProvider>().GetKeyedService<IIndexManager>(profile.ProviderName);
        if (manager is null) return Conflict($"No index manager is registered for provider '{profile.ProviderName}'.");
        return await manager.RebuildAsync(profile) ? Ok(IndexSummary.From(profile)) : BadRequest("The index could not be rebuilt.");
    }
}
