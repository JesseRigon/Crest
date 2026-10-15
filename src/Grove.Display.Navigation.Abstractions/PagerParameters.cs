using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Grove.Display.Navigation;

public class PagerParameters
{
    /// <summary>
    /// Gets or sets the current page number or null if none specified.
    /// </summary>
    [BindProperty(Name = "PageNum")]
    public int? Page { get; set; }

    /// <summary>
    /// Gets or sets the current page size or null if none specified.
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>Reads <c>pagenum</c> and <c>pagesize</c> from a query string (names are case-insensitive).</summary>
    public static PagerParameters FromQuery(IQueryCollection query)
    {
        var parameters = new PagerParameters();
        if (query is null)
        {
            return parameters;
        }

        parameters.Page = query.TryGetValue("PageNum", out var page) && int.TryParse(page, out var pageNum) ? pageNum : null;
        parameters.PageSize = query.TryGetValue(nameof(PageSize), out var size) && int.TryParse(size, out var pageSize) ? pageSize : null;

        return parameters;
    }
}
