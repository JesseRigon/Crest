using Grove.Contents.Localization.Models;
using Grove.Data.Indexing;

namespace Grove.Contents.Localization.Indexing;

public class LocalizationPartIndexHandler : ContentPartIndexHandler<LocalizationPart>
{
    public override Task BuildIndexAsync(LocalizationPart part, BuildPartIndexContext context)
    {
        var options = DocumentIndexOptions.Keyword | DocumentIndexOptions.Store;

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key + ".LocalizationSet", part.LocalizationSet, options);
            context.DocumentIndex.Set(key + ".Culture", part.Culture?.ToLowerInvariant(), options);
        }

        return Task.CompletedTask;
    }
}
