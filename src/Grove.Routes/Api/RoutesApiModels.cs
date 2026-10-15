namespace Grove.Routes.Api;

// The route management API's wire types, shared with the admin page (blazor-wasm links this
// file): plain records, no server types.

/// <summary>The API's paths.</summary>
public static class RoutesApiPaths
{
    public const string Base = "api/grove/routes";

    public const string Pages = Base + "/pages";

    public const string Home = Base + "/home";

    public const string ContentSources = Base + "/content-sources";

    public const string Content = Base + "/content";

    public const string Rules = Base + "/rules";

    public const string ReorderRules = Rules + "/reorder";
}

/// <summary>One page registry entry with its routes: what serves it and who may open it.</summary>
public sealed record PageRouteModel(
    string Id,
    string Shell,
    string[] Routes,
    string[] Permissions,
    string? Component,
    string? Source,
    string? Caption,
    bool AllowsAnonymous,
    bool ClientOnly,
    bool DeclaredBySource);

/// <summary>The site's home route: the route values the site root resolves to.</summary>
public sealed record HomeRouteModel(Dictionary<string, string?> RouteValues);

/// <summary>A kind of content route (Autoroute paths, aliases).</summary>
public sealed record ContentRouteSourceModel(string Name, string Caption);

public sealed record ContentRouteModel(string Path, string ContentItemId);

public sealed record ContentRoutesPage(ContentRouteModel[] Items, int Total);

/// <summary>A rewrite or redirect rule. <see cref="Order"/> is its 1-based place: rules apply in order.</summary>
public sealed record RewriteRuleModel(
    string Id,
    string Source,
    string Name,
    int Order,
    string? Pattern,
    string? SubstitutionPattern,
    bool IsCaseInsensitive,
    string QueryStringPolicy,
    string? RedirectType,
    bool SkipFurtherRules,
    DateTime CreatedUtc,
    string? Author);

/// <summary>A rule source a rule can be created from (the redirect rule, the rewrite rule).</summary>
public sealed record RewriteRuleSourceModel(string Name, string DisplayName, string? Description);

public sealed record RewriteRulesModel(RewriteRuleModel[] Rules, RewriteRuleSourceModel[] Sources);

/// <summary>
/// A rule as the page writes it. <see cref="Source"/> is read on create only. Query string
/// policies are <c>Append</c> or <c>Drop</c>; redirect types are <c>Found</c>,
/// <c>MovedPermanently</c>, <c>TemporaryRedirect</c> or <c>PermanentRedirect</c>.
/// </summary>
public sealed class RewriteRuleWrite
{
    public string? Source { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Pattern { get; set; } = string.Empty;

    public string SubstitutionPattern { get; set; } = string.Empty;

    public bool IsCaseInsensitive { get; set; }

    public string QueryStringPolicy { get; set; } = "Append";

    public string? RedirectType { get; set; }

    public bool SkipFurtherRules { get; set; }
}

/// <summary>Moves the rule at <see cref="OldIndex"/> to <see cref="NewIndex"/> (both 1-based).</summary>
public sealed record ReorderRulesRequest(int OldIndex, int NewIndex);
