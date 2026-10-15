using System.Text.Json.Nodes;
using Grove.Api;
using Grove.Entities;
using Grove.Environment.Shell;
using Grove.Routes.ContentRoutes;
using Grove.Routes.Models;
using Grove.Routes.Services;
using Grove.Routing;
using Grove.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Routes.Api;

/// <summary>
/// <c>api/grove/routes</c>: the tenant's routes for the route management page. The page registry's
/// entries, the home route and the content routes are read with <see cref="RoutesPermissions.ViewRoutes"/>;
/// the rewrite and redirect rules are read and written with
/// <see cref="UrlRewritingPermissions.ManageUrlRewritingRules"/>.
/// </summary>
public sealed class RoutesApi : ApiContributor
{
    public override string Name => "Routes";

    public override string FeatureId => "Grove.Routes";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("routes");
        routes.MapGet("pages", ApiHandlers.Handle<RoutesHandlers>(h => h.ListPagesAsync()));
        routes.MapGet("home", ApiHandlers.Handle<RoutesHandlers>(h => h.GetHomeAsync()));
        routes.MapGet("content-sources", ApiHandlers.Handle<RoutesHandlers>(h => h.ListContentSourcesAsync()));
        routes.MapGet("content", (HttpContext http, [FromQuery] string source, [FromQuery] string? search, [FromQuery] int? skip, [FromQuery] int? take) =>
            ApiHandlers.For<RoutesHandlers>(http).ListContentRoutesAsync(source, search, skip ?? 0, take ?? 50));

        routes.MapGet("rules", ApiHandlers.Handle<RoutesHandlers>(h => h.ListRulesAsync()));
        routes.MapPost("rules", (HttpContext http, [FromBody] RewriteRuleWrite request) => ApiHandlers.For<RoutesHandlers>(http).CreateRuleAsync(request));
        routes.MapPut("rules/{id}", (HttpContext http, [FromRoute] string id, [FromBody] RewriteRuleWrite request) => ApiHandlers.For<RoutesHandlers>(http).UpdateRuleAsync(id, request));
        routes.MapDelete("rules/{id}", (HttpContext http, [FromRoute] string id) => ApiHandlers.For<RoutesHandlers>(http).DeleteRuleAsync(id));
        routes.MapPost("rules/reorder", (HttpContext http, [FromBody] ReorderRulesRequest request) => ApiHandlers.For<RoutesHandlers>(http).ReorderRulesAsync(request));
    }
}

public sealed class RoutesHandlers(
    IAuthorizationService authorization,
    IPageRegistry pageRegistry,
    ISiteService siteService,
    IEnumerable<IContentRouteSource> contentRouteSources,
    IRewriteRulesManager rules,
    IEnumerable<IUrlRewriteRuleSource> ruleSources,
    IShellReleaseManager shellReleaseManager) : ApiHandlers
{
    public async Task<IResult> ListPagesAsync()
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var pages = await pageRegistry.GetAsync(HttpContext.RequestAborted);

        return Ok(pages.Entries
            .Where(entry => entry.Routes.Count > 0 || entry.Menu?.LegacyRoute is not null)
            .OrderBy(entry => entry.Shell.Value, StringComparer.Ordinal)
            .ThenBy(entry => entry.Routes.FirstOrDefault() ?? entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new PageRouteModel(
                entry.Id,
                entry.Shell.Value,
                [.. entry.Routes],
                [.. entry.Permissions],
                entry.Component?.FullName,
                entry.Component?.Assembly.GetName().Name ?? entry.DeclaredBy,
                entry.Menu?.Caption,
                entry.AllowsAnonymous,
                entry.ClientOnly,
                entry.DeclaredBySource))
            .ToArray());
    }

    public async Task<IResult> GetHomeAsync()
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var site = await siteService.GetSiteSettingsAsync();
        var values = site.HomeRoute is null
            ? []
            : site.HomeRoute.ToDictionary(entry => entry.Key, entry => entry.Value?.ToString(), StringComparer.OrdinalIgnoreCase);

        return Ok(new HomeRouteModel(values));
    }

    public async Task<IResult> ListContentSourcesAsync()
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(contentRouteSources.Select(source => new ContentRouteSourceModel(source.Name, source.Caption)).ToArray());
    }

    public async Task<IResult> ListContentRoutesAsync(string source, string? search, int skip, int take)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var routeSource = contentRouteSources.FirstOrDefault(candidate => string.Equals(candidate.Name, source, StringComparison.OrdinalIgnoreCase));
        if (routeSource is null)
        {
            return ApiProblems.NotFound($"There is no content route source named '{source}'.");
        }

        var (items, total) = await routeSource.QueryAsync(search, Math.Max(0, skip), Math.Clamp(take, 1, 200), HttpContext.RequestAborted);
        return Ok(new ContentRoutesPage([.. items.Select(item => new ContentRouteModel(item.Path, item.ContentItemId))], total));
    }

    public async Task<IResult> ListRulesAsync()
    {
        if (!await CanManageRulesAsync())
        {
            return Forbid();
        }

        return Ok(new RewriteRulesModel(
            [.. (await rules.GetAllAsync()).OrderBy(rule => rule.Order).Select(ToModel)],
            [.. ruleSources.Select(source => new RewriteRuleSourceModel(source.TechnicalName, source.DisplayName.Value, source.Description.Value))]));
    }

    public async Task<IResult> CreateRuleAsync(RewriteRuleWrite request)
    {
        if (!await CanManageRulesAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Source) || !ruleSources.Any(source => source.TechnicalName == request.Source))
        {
            return ApiProblems.Validation("source", $"There is no rule source named '{request.Source}'.");
        }

        var rule = await rules.NewAsync(request.Source, ToData(request));
        return await ValidateAndSaveAsync(rule!, created: true);
    }

    public async Task<IResult> UpdateRuleAsync(string id, RewriteRuleWrite request)
    {
        if (!await CanManageRulesAsync())
        {
            return Forbid();
        }

        var stored = await rules.FindByIdAsync(id);
        if (stored is null)
        {
            return NotFound();
        }

        // Work on a copy so a refused update leaves the cached rule untouched.
        var rule = stored.Clone();
        await rules.UpdateAsync(rule, ToData(request));
        return await ValidateAndSaveAsync(rule, created: false);
    }

    public async Task<IResult> DeleteRuleAsync(string id)
    {
        if (!await CanManageRulesAsync())
        {
            return Forbid();
        }

        var rule = await rules.FindByIdAsync(id);
        if (rule is null)
        {
            return NotFound();
        }

        await rules.DeleteAsync(rule);

        // The rewrite options are built with the shell: a change applies once the shell reloads.
        shellReleaseManager.RequestRelease();
        return NoContent();
    }

    public async Task<IResult> ReorderRulesAsync(ReorderRulesRequest request)
    {
        if (!await CanManageRulesAsync())
        {
            return Forbid();
        }

        await rules.ResortOrderAsync(request.OldIndex, request.NewIndex);
        shellReleaseManager.RequestRelease();
        return NoContent();
    }

    private async Task<IResult> ValidateAndSaveAsync(RewriteRule rule, bool created)
    {
        var result = await rules.ValidateAsync(rule);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .SelectMany(error => (error.MemberNames.Any() ? error.MemberNames : [string.Empty]).Select(member => (Key: WireName(member), Message: error.ErrorMessage ?? string.Empty)))
                .GroupBy(error => error.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray(), StringComparer.Ordinal);

            return ApiProblems.Validation(errors);
        }

        await rules.SaveAsync(rule);
        shellReleaseManager.RequestRelease();

        var model = ToModel(rule);
        return created ? Created($"{RoutesApiPaths.Rules}/{rule.Id}", model) : Ok(model);
    }

    // The rule handlers read the recipe step's shape: property names as the metadata declares them.
    private static JsonObject ToData(RewriteRuleWrite request)
    {
        var data = new JsonObject
        {
            [nameof(RewriteRule.Name)] = request.Name?.Trim(),
            [nameof(UrlRewriteSourceMetadata.Pattern)] = request.Pattern?.Trim(),
            [nameof(UrlRewriteSourceMetadata.SubstitutionPattern)] = request.SubstitutionPattern?.Trim(),
            [nameof(UrlRewriteSourceMetadata.IsCaseInsensitive)] = request.IsCaseInsensitive,
            [nameof(UrlRewriteSourceMetadata.QueryStringPolicy)] = Enum.TryParse<QueryStringPolicy>(request.QueryStringPolicy, ignoreCase: true, out var policy) ? policy.ToString() : nameof(QueryStringPolicy.Append),
            [nameof(UrlRewriteSourceMetadata.SkipFurtherRules)] = request.SkipFurtherRules,
        };

        if (Enum.TryParse<RedirectType>(request.RedirectType, ignoreCase: true, out var redirectType))
        {
            data[nameof(UrlRedirectSourceMetadata.RedirectType)] = redirectType.ToString();
        }

        return data;
    }

    private static RewriteRuleModel ToModel(RewriteRule rule)
    {
        if (rule.Source == UrlRedirectRuleSource.SourceName)
        {
            var redirect = rule.GetOrCreate<UrlRedirectSourceMetadata>();
            return new RewriteRuleModel(rule.Id, rule.Source, rule.Name, rule.Order, redirect.Pattern, redirect.SubstitutionPattern, redirect.IsCaseInsensitive,
                redirect.QueryStringPolicy.ToString(), (Enum.IsDefined(redirect.RedirectType) ? redirect.RedirectType : RedirectType.Found).ToString(), false, rule.CreatedUtc, rule.Author);
        }

        var rewrite = rule.GetOrCreate<UrlRewriteSourceMetadata>();
        return new RewriteRuleModel(rule.Id, rule.Source, rule.Name, rule.Order, rewrite.Pattern, rewrite.SubstitutionPattern, rewrite.IsCaseInsensitive,
            rewrite.QueryStringPolicy.ToString(), null, rewrite.SkipFurtherRules, rule.CreatedUtc, rule.Author);
    }

    private static string WireName(string member) =>
        string.IsNullOrEmpty(member) ? string.Empty : char.ToLowerInvariant(member[0]) + member[1..];

    private async Task<bool> CanViewAsync() =>
        await authorization.AuthorizeAsync(User, RoutesPermissions.ViewRoutes)
        || await authorization.AuthorizeAsync(User, UrlRewritingPermissions.ManageUrlRewritingRules);

    private Task<bool> CanManageRulesAsync() => authorization.AuthorizeAsync(User, UrlRewritingPermissions.ManageUrlRewritingRules);
}
