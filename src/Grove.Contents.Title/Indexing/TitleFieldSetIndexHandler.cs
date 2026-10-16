using Grove.Data.Indexing;
using Grove.Contents.Title.Models;

namespace Grove.Contents.Title.Indexing;

public class TitleFieldSetIndexHandler : ContentFieldSetIndexHandler<TitleFieldSet>
{
    public override Task BuildIndexAsync(TitleFieldSet part, BuildFieldSetIndexContext context)
    {
        var options = context.Settings.ToOptions();

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, part.Title, options);
        }

        return Task.CompletedTask;
    }
}
