using System.Reflection;
using Crest.Workflows.Http.ContentWriters;
using Crest.Workflows.UIHints.Dropdown;

namespace Crest.Workflows.Http.UIHints;

/// <summary>
/// Provides options for the <see cref="SendHttpRequest"/> activity's <see cref="SendHttpRequestBase.ContentType"/> property.
/// </summary>
public class HttpContentTypeOptionsProvider : DropDownOptionsProviderBase
{
    private readonly IEnumerable<IHttpContentFactory> _httpContentFactories;

    /// <summary>
    /// Creates a new instance of the <see cref="HttpContentTypeOptionsProvider"/> class.
    /// </summary>
    public HttpContentTypeOptionsProvider(IEnumerable<IHttpContentFactory> httpContentFactories)
    {
        _httpContentFactories = httpContentFactories;
    }

    /// <inheritdoc />
    protected override ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken)
    {
        var contentTypes = _httpContentFactories.SelectMany(x => x.SupportedContentTypes).Distinct().OrderBy(x => x).ToArray();
        var selectListItems = new List<SelectListItem> { new("", "") };

        selectListItems.AddRange(contentTypes.Select(x => new SelectListItem(x, x)));

        return new(selectListItems);
    }
}