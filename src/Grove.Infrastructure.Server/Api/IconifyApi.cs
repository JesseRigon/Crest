using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Display.Icons;
using Microsoft.AspNetCore.Authorization;

namespace Grove.Api;

/// <summary>The host's <c>api/grove/iconify</c> endpoints.</summary>
public sealed class IconifyApi : ApiContributor
{
    public override string Name => "Iconify";

    public override string FeatureId => "Grove";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("iconify");
        routes.MapGet("collections", (HttpContext http, [FromQuery] string? prefix, [FromQuery] string? prefixes) => ApiHandlers.For<IconifyHandlers>(http).CollectionsAsync(prefix, prefixes));
        routes.MapGet("collection", (HttpContext http, [FromQuery] string? prefix) => ApiHandlers.For<IconifyHandlers>(http).CollectionAsync(prefix));
        routes.MapGet("search", (HttpContext http, [FromQuery] string? query, [FromQuery] int start = 0, [FromQuery] int limit = 64, [FromQuery] string? prefix = null, [FromQuery] string? prefixes = null) => ApiHandlers.For<IconifyHandlers>(http).SearchAsync(query, start, limit, prefix, prefixes));
        routes.MapGet("{prefix}.json", (HttpContext http, [FromRoute] string prefix, [FromQuery] string? icons) => ApiHandlers.For<IconifyHandlers>(http).IconsAsync(prefix, icons));
    }
}

public sealed class IconifyHandlers(
    IAuthorizationService authorizationService,
    IIconRegistry iconRegistry) : ApiHandlers
{
    public async Task<IResult> CollectionsAsync(string? prefix, string? prefixes)
    {
        if (!await CanUseIconsAsync())
        {
            return Forbid();
        }

        var requested = RequestedPrefixes(prefix, prefixes);
        var libraries = await iconRegistry.GetLibrariesAsync(HttpContext.RequestAborted);
        var response = new JsonObject();
        foreach (var library in libraries.Where(library => requested.Count == 0 || requested.Contains(library.Id)))
        {
            response[library.Id] = ToIconifyInfo(library, 0);
        }

        return Ok(response);
    }

    public async Task<IResult> CollectionAsync(string? prefix)
    {
        if (!await CanUseIconsAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            return BadRequest();
        }

        var result = await iconRegistry.SearchAsync(new IconSearchRequest(prefix, null, 0, 200), HttpContext.RequestAborted);
        var names = result.Items.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return Ok(new IconifyCollectionResponse(prefix, result.Total, names));
    }

    public async Task<IResult> SearchAsync(string? query, int start = 0, int limit = 64, string? prefix = null, string? prefixes = null)
    {
        if (!await CanUseIconsAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest();
        }

        var requested = RequestedPrefixes(prefix, prefixes);
        var take = Math.Clamp(limit, 1, 200);
        var items = new List<IconSearchItem>();
        var total = 0;

        if (requested.Count == 0)
        {
            var result = await iconRegistry.SearchAsync(new IconSearchRequest(null, query, Math.Max(0, start), take), HttpContext.RequestAborted);
            items.AddRange(result.Items);
            total = result.Total;
        }
        else
        {
            foreach (var library in requested)
            {
                var result = await iconRegistry.SearchAsync(new IconSearchRequest(library, query, Math.Max(0, start), take), HttpContext.RequestAborted);
                items.AddRange(result.Items);
                total += result.Total;
            }
        }

        return Ok(new IconifySearchResponse(
            items.Select(item => $"{item.Library}:{item.Name}").Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            total,
            take,
            Math.Max(0, start)));
    }

    public async Task<IResult> IconsAsync(string prefix, string? icons)
    {
        if (!await CanUseIconsAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(icons))
        {
            return BadRequest();
        }

        var iconNames = icons.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var keys = iconNames.Select(name => IconKey.Create(prefix, "current", "default", name)).ToArray();
        var pack = await iconRegistry.BuildPackAsync(keys, HttpContext.RequestAborted);
        var response = new JsonObject
        {
            ["prefix"] = prefix,
            ["width"] = 16,
            ["height"] = 16,
            ["icons"] = new JsonObject(),
        };

        var iconsObject = (JsonObject)response["icons"]!;
        foreach (var item in pack.Icons.Values)
        {
            iconsObject[item.Name] = new JsonObject
            {
                ["body"] = ExtractSvgBody(item.SvgMarkup),
                ["width"] = 16,
                ["height"] = 16,
            };
        }

        return Ok(response);
    }

    private Task<bool> CanUseIconsAsync() => authorizationService.AuthorizeAsync(User, Grove.Contents.AdminMenu.AdminMenuPermissions.ManageAdminMenu);

    private static HashSet<string> RequestedPrefixes(string? prefix, string? prefixes)
    {
        var values = new[] { prefix }
            .Concat((prefixes ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant());

        return values.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static JsonObject ToIconifyInfo(IconLibraryDescriptor library, int total) => new()
    {
        ["name"] = library.Name,
        ["total"] = total,
        ["version"] = library.Version,
        ["category"] = library.ProviderName,
        ["palette"] = false,
    };

    private static string ExtractSvgBody(string svg)
    {
        try
        {
            using var document = JsonDocument.Parse("{}");
            var start = svg.IndexOf('>');
            var end = svg.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
            return start >= 0 && end > start ? svg[(start + 1)..end] : svg;
        }
        catch
        {
            return svg;
        }
    }

    private sealed record IconifyCollectionResponse(string Prefix, int Total, string[] Uncategorized);

    private sealed record IconifySearchResponse(string[] Icons, int Total, int Limit, int Start);
}
