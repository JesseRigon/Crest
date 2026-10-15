using Grove.Api;
using Grove.Data.Global;
using Grove.Data.Regions.Indexes;
using Grove.Data.Regions.Models;
using Grove.Data.Regions.Services;
using Grove.Environment.Shell;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using YesSql;
using YesSql.Services;

namespace Grove.Data.Regions.Api;

/// <summary>
/// <c>api/grove/regions/geo/import</c>, tree population tooling, Default tenant only
/// (docs/regions.md › Populating the tree): upserts standard nodes, postal codes and boundaries
/// into the global store from the same JSON shape the embedded data files use, or from a
/// Gazetteer, postal-code or GeoJSON file. The cycle check runs over the whole submitted batch
/// before anything is written.
/// </summary>
public sealed class GeoImportApi : ApiContributor
{
    private const long FileLimit = 512L * 1024 * 1024;

    public override string Name => "GeoImport";

    public override string FeatureId => RegionsConstants.FeatureId;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("regions/geo/import");
        routes.MapPost("", (HttpContext http, [FromBody] GeoImportBatch batch, CancellationToken cancellationToken) => ApiHandlers.For<GeoImportHandlers>(http).ImportAsync(batch, cancellationToken))
            .WithMetadata(new RequestSizeLimitAttribute(FileLimit));
        routes.MapPost("gazetteer", (HttpContext http, IFormFile file, [FromQuery] int? level, CancellationToken cancellationToken) => ApiHandlers.For<GeoImportHandlers>(http).ImportGazetteerAsync(file, level ?? 3, cancellationToken))
            .WithMetadata(new RequestSizeLimitAttribute(FileLimit))
            .DisableAntiforgery();
        routes.MapPost("postal-codes", (HttpContext http, IFormFile file, [FromQuery] string? format, CancellationToken cancellationToken) => ApiHandlers.For<GeoImportHandlers>(http).ImportPostalCodesAsync(file, format ?? PostalCodeFileParser.TableFormat, cancellationToken))
            .WithMetadata(new RequestSizeLimitAttribute(FileLimit))
            .DisableAntiforgery();
        routes.MapPost("boundaries", (HttpContext http, IFormFile file, [FromQuery] string? idProperty, [FromQuery] string? idFormat, CancellationToken cancellationToken) => ApiHandlers.For<GeoImportHandlers>(http).ImportBoundariesAsync(file, idProperty ?? "nodeId", idFormat, cancellationToken))
            .WithMetadata(new RequestSizeLimitAttribute(1024L * 1024 * 1024))
            .DisableAntiforgery();
    }
}

public sealed class GeoImportHandlers(IGlobalStore globalStore, ShellSettings shellSettings, IAuthorizationService authorization) : ApiHandlers
{
    public async Task<IResult> ImportAsync(GeoImportBatch batch, CancellationToken cancellationToken)
    {
        if (await RefuseAsync() is { } refusal) return refusal;

        return Ok(await WriteAsync(batch, cancellationToken));
    }

    /// <summary>A Census Gazetteer file (counties at level 3, or another level's file with ?level=) upserted as standard nodes.</summary>
    public async Task<IResult> ImportGazetteerAsync(IFormFile file, int level, CancellationToken cancellationToken)
    {
        if (await RefuseAsync() is { } refusal) return refusal;

        var batch = new GeoImportBatch();
        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            batch.Nodes.AddRange(CensusGazetteerParser.Read(reader, level));
        }

        return Ok(await WriteAsync(batch, cancellationToken));
    }

    /// <summary>
    /// A postal-code file (docs/regions.md › Populating the tree): <c>format=table</c> (a header
    /// row with Country, Code, NodeIds, Latitude, Longitude) or <c>format=geonames</c> (the
    /// GeoNames postal dump). No data ships; the file is the caller's, as is its licence.
    /// </summary>
    public async Task<IResult> ImportPostalCodesAsync(IFormFile file, string format, CancellationToken cancellationToken)
    {
        if (await RefuseAsync() is { } refusal) return refusal;

        var batch = new GeoImportBatch();
        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            batch.PostalCodes.AddRange(PostalCodeFileParser.Read(reader, format));
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(exception.Message);
        }

        return Ok(await WriteAsync(batch, cancellationToken));
    }

    /// <summary>
    /// Boundaries from a GeoJSON FeatureCollection: <c>idProperty</c> names the feature property
    /// holding the node id, <c>idFormat</c> composes it (<c>US-IL-{0}</c>). Stored as WKT
    /// MultiPolygons; features for nodes the tree lacks are skipped with a warning.
    /// </summary>
    public async Task<IResult> ImportBoundariesAsync(IFormFile file, string idProperty, string? idFormat, CancellationToken cancellationToken)
    {
        if (await RefuseAsync() is { } refusal) return refusal;

        var batch = new GeoImportBatch();
        var skipped = new List<string>();
        try
        {
            await using var stream = file.OpenReadStream();
            batch.Boundaries.AddRange(GeoJsonBoundaryParser.Read(stream, idProperty, idFormat, skipped));
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Text.Json.JsonException or ArgumentException or FormatException)
        {
            return BadRequest(exception.Message);
        }

        var result = await WriteAsync(batch, cancellationToken);
        result.Warnings.InsertRange(0, skipped.Take(MaxWarnings));
        return Ok(result);
    }

    private async Task<IResult?> RefuseAsync()
    {
        if (!shellSettings.IsDefaultShell()) return NotFound();
        return await authorization.AuthorizeAsync(User, GlobalPermissions.ManageGlobalReferenceData) ? null : Forbid();
    }

    private async Task<GeoImportResult> WriteAsync(GeoImportBatch batch, CancellationToken cancellationToken)
    {
        var result = new GeoImportResult();
        await globalStore.WriteAsync(async (session, ct) =>
        {
            var existing = (await session.Query<GeoNode, GeoNodeIndex>().ListAsync(ct)).ToDictionary(node => node.NodeId, StringComparer.Ordinal);

            // Acyclicity over the merged graph: existing parents plus the batch's, before any write.
            var parents = existing.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<string>)entry.Value.ParentIds, StringComparer.Ordinal);
            foreach (var node in batch.Nodes)
            {
                parents[node.NodeId] = node.ParentIds;
            }

            foreach (var node in batch.Nodes)
            {
                if (ReachesItself(node.NodeId, parents))
                {
                    result.Errors.Add($"Node '{node.NodeId}' would create a cycle through its parents.");
                }
            }

            if (result.Errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors));
            }

            foreach (var incoming in batch.Nodes)
            {
                if (existing.TryGetValue(incoming.NodeId, out var current))
                {
                    current.Country = incoming.Country;
                    current.Level = incoming.Level;
                    current.Kind = incoming.Kind;
                    current.Name = incoming.Name;
                    current.Code = incoming.Code;
                    current.Abbreviation = incoming.Abbreviation;
                    current.ParentIds = incoming.ParentIds;
                    current.GroupIds = incoming.GroupIds;
                    current.Latitude = incoming.Latitude;
                    current.Longitude = incoming.Longitude;
                    await session.SaveAsync(current);
                    result.NodesUpdated++;
                }
                else
                {
                    await session.SaveAsync(incoming);
                    existing[incoming.NodeId] = incoming;
                    result.NodesAdded++;
                }
            }

            // Postal codes, upserted by (country, code) as lookups normalize it. A node the tree
            // does not have is dropped from the code's set with a warning, never stored dangling.
            var countries = batch.PostalCodes.Select(code => code.Country).Distinct(StringComparer.Ordinal).ToArray();
            var storedCodes = countries.Length == 0
                ? new Dictionary<(string, string), GeoPostalCode>()
                : (await session.Query<GeoPostalCode, GeoPostalCodeIndex>(index => index.Country.IsIn(countries)).ListAsync(ct))
                    .GroupBy(code => (code.Country, code.Code))
                    .ToDictionary(group => group.Key, group => group.First());

            foreach (var incoming in batch.PostalCodes)
            {
                incoming.Code = PostalCodeFileParser.NormalizeCode(incoming.Code);
                var unknown = incoming.NodeIds.Where(nodeId => !existing.ContainsKey(nodeId)).ToArray();
                if (unknown.Length > 0)
                {
                    incoming.NodeIds = [.. incoming.NodeIds.Except(unknown, StringComparer.Ordinal)];
                    if (result.Warnings.Count < MaxWarnings)
                    {
                        result.Warnings.Add($"Postal code {incoming.Country} {incoming.Code}: no node {string.Join(", ", unknown)}.");
                    }
                }

                var current = storedCodes.GetValueOrDefault((incoming.Country, incoming.Code));
                if (current is null)
                {
                    await session.SaveAsync(incoming);
                    storedCodes[(incoming.Country, incoming.Code)] = incoming;
                }
                else
                {
                    current.NodeIds = incoming.NodeIds;
                    current.Latitude = incoming.Latitude;
                    current.Longitude = incoming.Longitude;
                    await session.SaveAsync(current);
                }

                result.PostalCodes++;
            }

            foreach (var incoming in batch.Boundaries)
            {
                if (!existing.ContainsKey(incoming.NodeId))
                {
                    if (result.Warnings.Count < MaxWarnings)
                    {
                        result.Warnings.Add($"Boundary for {incoming.NodeId} skipped: the tree has no such node.");
                    }

                    continue;
                }

                var current = await session.Query<GeoBoundary, GeoBoundaryIndex>(index => index.NodeId == incoming.NodeId).FirstOrDefaultAsync(ct);
                if (current is null)
                {
                    await session.SaveAsync(incoming);
                }
                else
                {
                    current.Format = incoming.Format;
                    current.Geometry = incoming.Geometry;
                    await session.SaveAsync(current);
                }

                result.Boundaries++;
            }
        }, cancellationToken);

        return result;
    }

    private const int MaxWarnings = 200;

    private static bool ReachesItself(string nodeId, IReadOnlyDictionary<string, IReadOnlyList<string>> parents)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(parents.TryGetValue(nodeId, out var direct) ? direct : []);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == nodeId)
            {
                return true;
            }

            if (seen.Add(current) && parents.TryGetValue(current, out var next))
            {
                foreach (var parent in next)
                {
                    stack.Push(parent);
                }
            }
        }

        return false;
    }
}

public sealed class GeoImportBatch
{
    public List<GeoNode> Nodes { get; set; } = [];
    public List<GeoPostalCode> PostalCodes { get; set; } = [];
    public List<GeoBoundary> Boundaries { get; set; } = [];
}

public sealed class GeoImportResult
{
    public int NodesAdded { get; set; }
    public int NodesUpdated { get; set; }
    public int PostalCodes { get; set; }
    public int Boundaries { get; set; }
    public List<string> Errors { get; set; } = [];

    /// <summary>Rows that were imported only in part or skipped (at most 200 listed).</summary>
    public List<string> Warnings { get; set; } = [];
}
