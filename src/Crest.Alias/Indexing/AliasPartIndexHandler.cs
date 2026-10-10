using Crest.Alias.Models;
using Crest.Indexing;

namespace Crest.Alias.Indexing;

public class AliasPartIndexHandler : ContentPartIndexHandler<AliasPart>
{
    public override Task BuildIndexAsync(AliasPart part, BuildPartIndexContext context)
    {
        var options = DocumentIndexOptions.Keyword | DocumentIndexOptions.Store;

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, part.Alias, options);
        }

        return Task.CompletedTask;
    }
}
