using Grove.Contents.Fields.Fields;
using Grove.Data.Indexing;

namespace Grove.Contents.Fields.Indexing;

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
