using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Primitives;

namespace Grove.Routing;

/// <summary>
/// Whether a legacy (non-Blazor) endpoint owns a site path: an MVC action, a conventional
/// route, a hub - anything the tenant's endpoint data source maps that is not a shell page.
/// </summary>
/// <remarks>
/// One routing system (ruling 2026-10-13, docs/shells-and-themes.md › Shell dispatch): the
/// shell middleware resolves every site path itself and forwards a URL to the legacy MVC
/// shell when a legacy page owns it, until MVC is gone. Endpoint routing is never left to
/// referee between a Blazor catch-all and MVC or static files. The site's content page is
/// therefore "/" only, and this answers "does a legacy endpoint own this path" before the
/// middleware rewrites a content path to it.
/// </remarks>
public interface ILegacyRouteOwnership
{
    bool Owns(HttpContext context, PathString path);
}

/// <summary>
/// Reads the tenant's <see cref="EndpointDataSource"/>: every <see cref="RouteEndpoint"/>
/// without Blazor's <c>ComponentTypeMetadata</c>, except the static-assets development fallback
/// (<c>{**path:file}</c>). Matched with the public template matcher plus the endpoint's required
/// values (what makes a conventional area route per action) and inline constraints. Compiled
/// once per endpoint set and rebuilt on the data source's change token; prefiltered by the
/// pattern's first literal segment so a check is a dictionary lookup and a few matches.
/// </summary>
public sealed class LegacyRouteOwnership(EndpointDataSource endpoints, IInlineConstraintResolver constraints) : ILegacyRouteOwnership
{
    private const string StaticAssetsFallback = "{**path:file}";

    private Compiled? _compiled;
    private readonly Lock _gate = new();

    public bool Owns(HttpContext context, PathString path)
    {
        var compiled = Current();
        var value = path.Value ?? string.Empty;
        var first = FirstSegment(value);
        var candidates = compiled.ByFirstLiteral.TryGetValue(first, out var literal)
            ? literal.Concat(compiled.ParametricFirst)
            : compiled.ParametricFirst;

        foreach (var candidate in candidates)
        {
            if (candidate.Matches(context, path))
            {
                return true;
            }
        }

        return false;
    }

    private Compiled Current()
    {
        var compiled = _compiled;
        if (compiled is not null && !compiled.Token.HasChanged)
        {
            return compiled;
        }

        lock (_gate)
        {
            if (_compiled is null || _compiled.Token.HasChanged)
            {
                _compiled = Compile(endpoints, constraints);
            }

            return _compiled;
        }
    }

    internal static Compiled Compile(EndpointDataSource source, IInlineConstraintResolver constraints)
    {
        var token = source.GetChangeToken();
        var byFirstLiteral = new Dictionary<string, List<Candidate>>(StringComparer.OrdinalIgnoreCase);
        var parametricFirst = new List<Candidate>();

        foreach (var endpoint in source.Endpoints.OfType<RouteEndpoint>())
        {
            if (!IsLegacy(endpoint) || Candidate.From(endpoint, constraints) is not { } candidate)
            {
                continue;
            }

            var firstSegment = endpoint.RoutePattern.PathSegments.FirstOrDefault();
            if (firstSegment is { Parts: [Microsoft.AspNetCore.Routing.Patterns.RoutePatternLiteralPart literal] })
            {
                (byFirstLiteral.TryGetValue(literal.Content, out var list) ? list : byFirstLiteral[literal.Content] = []).Add(candidate);
            }
            else
            {
                parametricFirst.Add(candidate);
            }
        }

        return new Compiled(token, byFirstLiteral, parametricFirst);
    }

    /// <summary>A legacy endpoint: a route endpoint that is not a shell page, nor the static-assets fallback.</summary>
    internal static bool IsLegacy(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Components.Endpoints.ComponentTypeMetadata>() is null
        && endpoint.Metadata.GetMetadata<ThemeOwnerMetadata>() is null
        && !string.Equals(endpoint.RoutePattern.RawText, StaticAssetsFallback, StringComparison.Ordinal);

    private static string FirstSegment(string path)
    {
        var trimmed = path.TrimStart('/');
        var slash = trimmed.IndexOf('/');
        return slash < 0 ? trimmed : trimmed[..slash];
    }

    internal sealed record Compiled(IChangeToken Token, Dictionary<string, List<Candidate>> ByFirstLiteral, List<Candidate> ParametricFirst);

    internal sealed class Candidate
    {
        private readonly TemplateMatcher _matcher;
        private readonly IReadOnlyDictionary<string, object?> _requiredValues;
        private readonly (string Key, IRouteConstraint Constraint)[] _constraints;

        private Candidate(TemplateMatcher matcher, IReadOnlyDictionary<string, object?> requiredValues, (string, IRouteConstraint)[] constraints)
        {
            _matcher = matcher;
            _requiredValues = requiredValues;
            _constraints = constraints;
        }

        public static Candidate? From(RouteEndpoint endpoint, IInlineConstraintResolver constraints)
        {
            if (endpoint.RoutePattern.RawText is not { } text)
            {
                return null;
            }

            RouteTemplate template;
            try
            {
                template = TemplateParser.Parse(text);
            }
            catch (ArgumentException)
            {
                return null;
            }

            var defaults = new RouteValueDictionary(endpoint.RoutePattern.Defaults);
            var resolved = new List<(string, IRouteConstraint)>();
            foreach (var parameter in template.Parameters)
            {
                foreach (var inline in parameter.InlineConstraints)
                {
                    // An unknown constraint is not a reason to drop the endpoint: it matches
                    // on the template alone, which errs toward the legacy owner.
                    if (constraints.ResolveConstraint(inline.Constraint) is { } constraint && parameter.Name is { } name)
                    {
                        resolved.Add((name, constraint));
                    }
                }
            }

            return new Candidate(new TemplateMatcher(template, defaults), endpoint.RoutePattern.RequiredValues, [.. resolved]);
        }

        public bool Matches(HttpContext context, PathString path)
        {
            var values = new RouteValueDictionary();
            if (!_matcher.TryMatch(path, values))
            {
                return false;
            }

            // A conventional route is one endpoint per action, told apart by its required
            // values (area, controller, action): the matched or defaulted value must agree.
            foreach (var (key, required) in _requiredValues)
            {
                values.TryGetValue(key, out var actual);
                var requiredText = required?.ToString();
                var actualText = actual?.ToString();
                if (string.IsNullOrEmpty(requiredText) ? !string.IsNullOrEmpty(actualText)
                    : !string.Equals(requiredText, actualText, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            foreach (var (key, constraint) in _constraints)
            {
                if (!constraint.Match(context, null, key, values, RouteDirection.IncomingRequest))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
