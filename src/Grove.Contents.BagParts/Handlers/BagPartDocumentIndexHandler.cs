using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.BagParts.Models;
using Grove.Data.Indexing;

namespace Grove.Contents.BagParts;

internal sealed class BagPartDocumentIndexHandler : ContentPartIndexHandler<BagPart>
{
    public override Task BuildIndexAsync(BagPart part, BuildPartIndexContext context)
    {
        if (!context.Settings.Included)
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(context.ContentTypePartDefinition.Name, part, DocumentIndexOptions.Store, new Dictionary<string, object>
        {
            { nameof(ContentTypePartDefinition), context.ContentTypePartDefinition },
        });

        return Task.CompletedTask;
    }
}
