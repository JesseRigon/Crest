using Crest.ContentFields.Fields;
using Crest.Indexing;

namespace Crest.ContentFields.Indexing;

public class YoutubeFieldIndexHandler : ContentFieldIndexHandler<YoutubeField>
{
    public override Task BuildIndexAsync(YoutubeField field, BuildFieldIndexContext context)
    {
        var options = context.Settings.ToOptions();

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, field.EmbeddedAddress, options);
        }

        return Task.CompletedTask;
    }
}
