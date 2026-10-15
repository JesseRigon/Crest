using Grove.Contents.Fields.Fields;
using Grove.Data.Indexing;

namespace Grove.Contents.Fields.Indexing;

public class LinkFieldIndexHandler : ContentFieldIndexHandler<LinkField>
{
    public override Task BuildIndexAsync(LinkField field, BuildFieldIndexContext context)
    {
        var options = context.Settings.ToOptions();

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, field.Url, options);
            context.DocumentIndex.Set(key, field.Text, options);
            context.DocumentIndex.Set(key, field.Target, options);
        }

        return Task.CompletedTask;
    }
}
