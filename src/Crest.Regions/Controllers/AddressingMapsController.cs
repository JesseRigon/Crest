using Crest.Data.Global;
using Crest.Environment.Shell;
using Crest.Regions.Global;
using Crest.Regions.Indexes;
using Crest.Regions.Models;
using Crest.Regions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YesSql;

namespace Crest.Regions.Controllers;

/// <summary>
/// Addressing maps as runtime rules (docs/regions.md › Addressing maps as runtime rules): the
/// maps live in the global store, every tenant reads them at runtime (the client from here,
/// the server through <see cref="IGeoService"/>), and the Default tenant edits them. An edited
/// map survives data-file reloads until it is reverted to the shipped one.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route(RegionsConstants.Routes.Api + "/addressing-maps")]
public sealed class AddressingMapsController(IGlobalStore globalStore, ShellSettings shellSettings, IAuthorizationService authorization, IGeoService geo) : ControllerBase
{
    /// <summary>The map that applies to a country (the generic map when it has none), for an address form.</summary>
    [HttpGet("{country}")]
    [Authorize]
    public async Task<ActionResult<AddressingMap>> GetAsync(string country, CancellationToken cancellationToken) =>
        Ok(await geo.GetAddressingMapAsync(country.Trim().ToUpperInvariant(), cancellationToken));

    /// <summary>Every stored map, for the editor: Default tenant, <c>ManageGlobalReferenceData</c>.</summary>
    [HttpGet]
    public async Task<ActionResult<AddressingMapSummary[]>> ListAsync(CancellationToken cancellationToken)
    {
        if (await RefuseAsync() is { } refusal) return refusal;

        var maps = await globalStore.ReadAsync((session, ct) => session.Query<AddressingMap, AddressingMapIndex>().ListAsync(ct), cancellationToken);
        return Ok(maps
            .OrderBy(map => map.Country == AddressingMapDefaults.AnyCountry ? 0 : 1)
            .ThenBy(map => map.Country, StringComparer.Ordinal)
            .Select(map => new AddressingMapSummary(map.Country, map.Edited, map.Levels.Count, map.PostalCodeLabel, map.PostalCodePattern))
            .ToArray());
    }

    /// <summary>Creates or replaces a country's map; it is marked edited.</summary>
    [HttpPut("{country}")]
    public async Task<ActionResult<AddressingMapWriteResult>> SaveAsync(string country, [FromBody] AddressingMap map, CancellationToken cancellationToken)
    {
        if (await RefuseAsync() is { } refusal) return refusal;

        map.Country = country.Trim() == AddressingMapDefaults.AnyCountry ? AddressingMapDefaults.AnyCountry : country.Trim().ToUpperInvariant();
        var errors = AddressingMapValidator.Validate(map);
        if (errors.Count > 0) return BadRequest(new AddressingMapWriteResult(false, errors.ToArray()));

        await globalStore.WriteAsync(async (session, ct) =>
        {
            var current = await session.Query<AddressingMap, AddressingMapIndex>(index => index.Country == map.Country).FirstOrDefaultAsync(ct);
            var target = current ?? new AddressingMap { Country = map.Country };
            target.Levels = map.Levels;
            target.Fields = map.Fields;
            target.PostalCodePattern = string.IsNullOrWhiteSpace(map.PostalCodePattern) ? null : map.PostalCodePattern;
            target.PostalCodeLabel = string.IsNullOrWhiteSpace(map.PostalCodeLabel) ? "Postal code" : map.PostalCodeLabel;
            target.LocalityLevel = map.LocalityLevel;
            target.Edited = true;
            await session.SaveAsync(target);
        }, cancellationToken);

        return Ok(new AddressingMapWriteResult(true, []));
    }

    /// <summary>Reverts a country's map to the shipped one; a country the data files do not know loses its map (the generic map then applies).</summary>
    [HttpDelete("{country}")]
    public async Task<ActionResult<AddressingMapWriteResult>> RevertAsync(string country, CancellationToken cancellationToken)
    {
        if (await RefuseAsync() is { } refusal) return refusal;

        var code = country.Trim() == AddressingMapDefaults.AnyCountry ? AddressingMapDefaults.AnyCountry : country.Trim().ToUpperInvariant();
        var shipped = GeoGlobalSchema.ShippedMap(code);
        if (shipped is null && code == AddressingMapDefaults.AnyCountry) return BadRequest(new AddressingMapWriteResult(false, ["The generic map has no shipped version to revert to."]));

        var found = false;
        await globalStore.WriteAsync(async (session, ct) =>
        {
            var current = await session.Query<AddressingMap, AddressingMapIndex>(index => index.Country == code).FirstOrDefaultAsync(ct);
            if (current is null)
            {
                return;
            }

            found = true;
            if (shipped is null)
            {
                session.Delete(current);
                return;
            }

            current.Levels = shipped.Levels;
            current.Fields = shipped.Fields;
            current.PostalCodePattern = shipped.PostalCodePattern;
            current.PostalCodeLabel = shipped.PostalCodeLabel;
            current.LocalityLevel = shipped.LocalityLevel;
            current.Edited = false;
            await session.SaveAsync(current);
        }, cancellationToken);

        return found ? Ok(new AddressingMapWriteResult(true, [])) : NotFound();
    }

    private async Task<ActionResult?> RefuseAsync()
    {
        if (!shellSettings.IsDefaultShell()) return NotFound();
        return await authorization.AuthorizeAsync(User, GlobalPermissions.ManageGlobalReferenceData) ? null : Forbid();
    }
}

public sealed record AddressingMapSummary(string Country, bool Edited, int LevelCount, string PostalCodeLabel, string? PostalCodePattern);

public sealed record AddressingMapWriteResult(bool Succeeded, string[] Errors);
