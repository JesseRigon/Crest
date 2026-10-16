using Grove.Contents.Localization.Models;
using Grove.Data.Indexing;

namespace Grove.Contents.Localization.Indexing;

public class LocalizationFieldSetIndexHandler : ContentFieldSetIndexHandler<LocalizationFieldSet>
{
    public override Task BuildIndexAsync(LocalizationFieldSet part, BuildFieldSetIndexContext context)
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
