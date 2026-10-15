using Microsoft.AspNetCore.Http;

namespace Grove.Display.Navigation;

/// <summary>
/// Represents the paging parameters of a safe navigation that doesn't
/// require counting.
/// </summary>
public class PagerSlimParameters
{
    /// <summary>
    /// Gets or sets the first item displayed on the page.
    /// </summary>
    public string Before { get; set; }

    /// <summary>
    /// Gets or sets the last item displayed on the page.
    /// </summary>
    public string After { get; set; }

    /// <summary>
    /// Gets or sets the requested page size, or <c>null</c> when none is specified.
    /// Only honored when the site allows page size selection and the value is one of the configured options.
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>Reads <c>before</c>, <c>after</c> and <c>pagesize</c> from a query string (names are case-insensitive).</summary>
    public static PagerSlimParameters FromQuery(IQueryCollection query)
    {
        var parameters = new PagerSlimParameters();
        if (query is null)
        {
            return parameters;
        }

        parameters.Before = query.TryGetValue(nameof(Before), out var before) ? before.ToString() : null;
        parameters.After = query.TryGetValue(nameof(After), out var after) ? after.ToString() : null;
        parameters.PageSize = query.TryGetValue(nameof(PageSize), out var size) && int.TryParse(size, out var pageSize) ? pageSize : null;

        return parameters;
    }
}
