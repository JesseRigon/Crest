using Crest.Workflows.Http.Contexts;
using Crest.Workflows.Http.Options;

namespace Crest.Workflows.Http.Services;

/// <inheritdoc />
public class DefaultDownloadableManager : IDownloadableManager
{
    private readonly IEnumerable<IDownloadableContentHandler> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDownloadableManager"/> class.
    /// </summary>
    public DefaultDownloadableManager(IEnumerable<IDownloadableContentHandler> providers)
    {
        _providers = providers.OrderBy(x => x.Priority).ToList();
    }

    /// <inheritdoc />
    public IEnumerable<Func<ValueTask<Downloadable>>> GetDownloadablesAsync(object content, DownloadableOptions? options = default, CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(x => x.GetSupportsContent(content));

        if (provider == null)
            return [];

        options ??= new();
        var context = new DownloadableContext(this, content, options, cancellationToken);
        var downloadables = provider.GetDownloadablesAsync(context);

        return downloadables;
    }
}