namespace Grove.Routing;

/// <summary>
/// The page registry's one route template grammar: a template is relative to its shell
/// (<c>/Features</c>, <c>/Contents/ContentItems/{ContentItemId}/Edit</c>), a <c>{segment}</c>
/// matches any one segment. The route table and the route gate both match with it.
/// </summary>
public static class RouteTemplates
{
    /// <summary>Whether <paramref name="path"/> matches <paramref name="template"/>: segment by segment, ignoring case, a <c>{segment}</c> matching any one.</summary>
    public static bool Matches(string template, string? path)
    {
        var templateSegments = template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = (path ?? string.Empty).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (templateSegments.Length != pathSegments.Length)
        {
            return false;
        }

        for (var index = 0; index < templateSegments.Length; index++)
        {
            var segment = templateSegments[index];
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                continue;
            }

            if (!string.Equals(segment, pathSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>How many literal segments a template has: the more, the more specific (a kind's own page over its registry's generic one).</summary>
    public static int Specificity(string template) =>
        template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Count(segment => !(segment.StartsWith('{') && segment.EndsWith('}')));
}
