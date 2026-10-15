#nullable enable
using Grove.ContentManagement.Handlers;

namespace Grove.Media.Library;

/// <summary>
/// Content arriving by recipe or deployment (ruling F1): its media fields' paths are resolved in
/// this tenant's library and ids that belong to another tenant are dropped, so an export carries
/// its media by path and lands on this tenant's placements. A path not (yet) in the library is
/// kept as a path, and the import step's rewrite fills it in later.
/// </summary>
public sealed class MediaFieldImportHandler(IMediaFieldRewriter rewriter, IMediaLibrary library) : ContentHandlerBase
{
    public override async Task ImportingAsync(ImportContentContext context)
    {
        if (await library.IsActiveAsync())
        {
            await rewriter.RewriteAsync(context.ContentItem);
        }
    }
}
