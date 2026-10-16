using Grove.Contents.Alias.Models;
using Grove.Data.Indexing;

namespace Grove.Contents.Alias.Indexing;

public class AliasFieldSetIndexHandler : ContentFieldSetIndexHandler<AliasFieldSet>
{
    public override Task BuildIndexAsync(AliasFieldSet part, BuildFieldSetIndexContext context)
    {
        var options = DocumentIndexOptions.Keyword | DocumentIndexOptions.Store;

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, part.Alias, options);
        }

        return Task.CompletedTask;
    }
}
