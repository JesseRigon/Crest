using Crest.ContentManagement.Metadata.Models;
using Crest.Flows.Models;
using Crest.Indexing;

namespace Crest.Flows;

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
