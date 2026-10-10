using System.Text;
using Crest.Workflows.Http.Abstractions;
using Crest.Workflows.Http.Contexts;

namespace Crest.Workflows.Http.DownloadableContentHandlers;

/// <summary>
/// Handles content that represents a downloadable string file.
/// </summary>
public class StringDownloadableContentHandler : DownloadableContentHandlerBase
{
    /// <inheritdoc />
    public override bool GetSupportsContent(object content) => content is string;

    /// <inheritdoc />
    protected override Downloadable GetDownloadable(DownloadableContext context)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes((string)context.Content));
        return new(stream, "file.txt", "text/plain");
    }
}
