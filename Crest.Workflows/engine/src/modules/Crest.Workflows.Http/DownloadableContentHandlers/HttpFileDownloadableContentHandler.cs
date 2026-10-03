using Crest.Workflows.Http.Abstractions;
using Crest.Workflows.Http.Contexts;

namespace Crest.Workflows.Http.DownloadableContentHandlers;

/// <summary>
/// Handles content that represents a downloadable stream.
/// </summary>
public class HttpFileDownloadableContentHandler : DownloadableContentHandlerBase
{
    /// <inheritdoc />
    public override bool GetSupportsContent(object content) => content is HttpFile;
    
    /// <inheritdoc />
    protected override Downloadable GetDownloadable(DownloadableContext context)
    {
        var file = (HttpFile)context.Content;
        var stream = file.Stream;
        var fileName = file.Filename;
        var contentType = file.ContentType;
        return new Downloadable(stream, fileName, contentType);
    }
}