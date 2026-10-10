using Crest.Workflows.Http.Abstractions;
using Crest.Workflows.Http.Contexts;

namespace Crest.Workflows.Http.DownloadableContentHandlers;

/// <summary>
/// Handles content that represents a downloadable binary file.
/// </summary>
public class BinaryDownloadableContentHandler : DownloadableContentHandlerBase
{
    /// <inheritdoc />
    public override bool GetSupportsContent(object content) => content is byte[];

    /// <inheritdoc />
    protected override Downloadable GetDownloadable(DownloadableContext context)
    {
        var bytes = (byte[]) context.Content;
        var stream = new MemoryStream(bytes);
        var fileName = "file.bin";
        var contentType = "application/octet-stream";
        return new(stream, fileName, contentType);
    }
}