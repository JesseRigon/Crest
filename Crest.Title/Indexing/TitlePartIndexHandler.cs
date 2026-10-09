using Crest.Indexing;
using Crest.Title.Models;

namespace Crest.Title.Indexing;

public class TitlePartIndexHandler : ContentPartIndexHandler<TitlePart>
{
    public override Task BuildIndexAsync(TitlePart part, BuildPartIndexContext context)
    {
        var options = context.Settings.ToOptions();

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, part.Title, options);
        }

        return Task.CompletedTask;
    }
}
