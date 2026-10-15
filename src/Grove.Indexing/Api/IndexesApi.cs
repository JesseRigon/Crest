#nullable enable

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Validation;
using Microsoft.Extensions.DependencyInjection;
using Grove.Indexing;
using System.Text.Json;
using Grove.Infrastructure.Entities;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Grove.Indexing.Models;

namespace Grove.Indexing.Api;

public sealed class IndexesApi : ApiContributor
{
    public override string Name => "Indexes";

    public override string FeatureId => "Grove.Indexing";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("indexes");
        routes.MapGet("", ApiHandlers.Handle<IndexesHandlers>(h => h.ListAsync()));
        routes.MapPost("{id}/rebuild", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<IndexesHandlers>(__http).RebuildAsync(id));
        routes.MapGet("sources", ApiHandlers.Handle<IndexesHandlers>(h => h.SourcesAsync()));
        routes.MapGet("{id}", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<IndexesHandlers>(__http).GetAsync(id));
        routes.MapPost("", (HttpContext __http, [FromBody] IndexProfileWrite request) => ApiHandlers.For<IndexesHandlers>(__http).CreateAsync(request));
        routes.MapPut("{id}", (HttpContext __http, [FromRoute] string id, [FromBody] IndexProfileWrite request) => ApiHandlers.For<IndexesHandlers>(__http).UpdateAsync(id, request));
        routes.MapDelete("{id}", (HttpContext __http, [FromRoute] string id, [FromQuery] bool force = false) => ApiHandlers.For<IndexesHandlers>(__http).DeleteAsync(id, force));
        routes.MapPost("{id}/reset", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<IndexesHandlers>(__http).ResetAsync(id));
        routes.MapPost("{id}/synchronize", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<IndexesHandlers>(__http).SynchronizeAsync(id));
    }
}

public sealed class IndexesHandlers(IRequestAccess requestAccess) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, IndexingPermissions.ManageIndexes);
        if (access is null) return Forbid();
        var profiles = access.GetRequiredService<IIndexProfileStore>();
        var items = await profiles.GetAllAsync();
        return Ok(items.OrderBy(profile => profile.Name).Select(IndexSummary.From).ToArray());
    }

    public async Task<IResult> RebuildAsync(string id)
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
    /// <summary>The index kinds the enabled providers offer (provider + type), for the create form.</summary>
    public async Task<IResult> SourcesAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, IndexingPermissions.ManageIndexes);
        if (access is null) return Forbid();
        var options = access.GetRequiredService<IOptions<IndexingOptions>>().Value;
        return Ok(options.Sources.Values
            .Select(entry => new IndexSource(entry.ProviderName, entry.Type, entry.DisplayName.Value, entry.Description?.Value))
            .OrderBy(source => source.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray());
    }

    /// <summary>The profile as stored (its JSON, the same shape a recipe step writes).</summary>
    public async Task<IResult> GetAsync(string id)
    {
        var access = await requestAccess.AuthorizeAsync(User, IndexingPermissions.ManageIndexes);
        if (access is null) return Forbid();
        var profile = await access.GetRequiredService<IIndexProfileManager>().FindByIdAsync(id);
        return profile is null ? NotFound() : Ok(JsonSerializer.SerializeToNode(profile));
    }

    /// <summary>Creates a profile from its JSON (as a recipe would), creates the index in the provider and starts synchronizing.</summary>
    public async Task<IResult> CreateAsync([FromBody] IndexProfileWrite request)
    {
        var access = await requestAccess.AuthorizeAsync(User, IndexingPermissions.ManageIndexes);
        if (access is null) return Forbid();
        var manager = access.GetRequiredService<IIndexProfileManager>();
        var options = access.GetRequiredService<IOptions<IndexingOptions>>().Value;
        if (!options.Sources.ContainsKey(new IndexProfileKey(request.ProviderName!, request.Type!)))
        {
            return BadRequest("Unknown provider or index type.");
        }

        var profile = await manager.NewAsync(request.ProviderName, request.Type, request.Data ?? new JsonObject());
        if (profile is null) return BadRequest("Invalid provider or type.");
        if (Invalid(await manager.ValidateAsync(profile)) is { } problem) return problem;

        var indexes = access.GetRequiredService<IServiceProvider>().GetKeyedService<IIndexManager>(profile.ProviderName);
        if (indexes is null) return Conflict($"No index manager is registered for provider '{profile.ProviderName}'.");

        // Stored locally first so every property is set, removed again when the provider refuses.
        await manager.CreateAsync(profile);
        if (!await indexes.CreateAsync(profile))
        {
            await manager.DeleteAsync(profile);
            return BadRequest($"The provider '{profile.ProviderName}' could not create the index.");
        }

        await manager.SynchronizeAsync(profile);
        return Ok(IndexSummary.From(profile));
    }

    public async Task<IResult> UpdateAsync(string id, [FromBody] IndexProfileWrite request)
    {
        var access = await requestAccess.AuthorizeAsync(User, IndexingPermissions.ManageIndexes);
        if (access is null) return Forbid();
        var manager = access.GetRequiredService<IIndexProfileManager>();
        var profile = await manager.FindByIdAsync(id);
        if (profile is null) return NotFound();

        await manager.UpdateAsync(profile, request.Data ?? new JsonObject());
        if (Invalid(await manager.ValidateAsync(profile)) is { } problem) return problem;
        return Ok(IndexSummary.From(profile));
    }

    /// <summary>Deletes the index in the provider and the profile; force removes the profile even when the provider cannot.</summary>
    public async Task<IResult> DeleteAsync(string id, [FromQuery] bool force = false)
    {
        var access = await requestAccess.AuthorizeAsync(User, IndexingPermissions.ManageIndexes);
        if (access is null) return Forbid();
        var manager = access.GetRequiredService<IIndexProfileManager>();
        var profile = await manager.FindByIdAsync(id);
        if (profile is null) return NotFound();

        var indexes = access.GetRequiredService<IServiceProvider>().GetKeyedService<IIndexManager>(profile.ProviderName);
        if (!force)
        {
            if (indexes is null) return Conflict($"No index manager is registered for provider '{profile.ProviderName}'.");
            if (await indexes.ExistsAsync(profile.IndexFullName) && !await indexes.DeleteAsync(profile))
            {
                return BadRequest($"The provider '{profile.ProviderName}' could not delete the index; force-delete removes the profile anyway.");
            }
        }
        else if (indexes is not null)
        {
            await indexes.DeleteAsync(profile);
        }

        return await manager.DeleteAsync(profile) ? NoContent() : BadRequest("The profile could not be deleted; try force-deleting it.");
    }

    /// <summary>Resets the index's progress and synchronizes it from the start.</summary>
    public async Task<IResult> ResetAsync(string id)
    {
        var access = await requestAccess.AuthorizeAsync(User, IndexingPermissions.ManageIndexes);
        if (access is null) return Forbid();
        var manager = access.GetRequiredService<IIndexProfileManager>();
        var profile = await manager.FindByIdAsync(id);
        if (profile is null) return NotFound();
        await manager.ResetAsync(profile);
        await manager.UpdateAsync(profile);
        await manager.SynchronizeAsync(profile);
        return Ok(IndexSummary.From(profile));
    }

    /// <summary>Starts synchronizing the index in the background.</summary>
    public async Task<IResult> SynchronizeAsync(string id)
    {
        var access = await requestAccess.AuthorizeAsync(User, IndexingPermissions.ManageIndexes);
        if (access is null) return Forbid();
        var manager = access.GetRequiredService<IIndexProfileManager>();
        var profile = await manager.FindByIdAsync(id);
        if (profile is null) return NotFound();
        await manager.SynchronizeAsync(profile);
        return Ok(IndexSummary.From(profile));
    }

    private static IResult? Invalid(ValidationResultDetails result) =>
        result.Succeeded ? null : BadRequest(result.Errors.Select(error => error.ErrorMessage).ToArray());
}

/// <summary>An index kind a provider offers.</summary>
public sealed record IndexSource(string ProviderName, string Type, string DisplayName, string? Description);

/// <summary>An index profile write: provider and type on create, and the profile's JSON (recipe shape).</summary>
public sealed record IndexProfileWrite([property: NotEmpty] string? ProviderName, [property: NotEmpty] string? Type, JsonObject? Data);
