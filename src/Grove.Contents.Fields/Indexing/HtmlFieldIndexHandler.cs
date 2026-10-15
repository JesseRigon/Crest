using Grove.Contents.Fields.Fields;
using Grove.Data.Indexing;

namespace Grove.Contents.Fields.Indexing;

public class HtmlFieldIndexHandler : ContentFieldIndexHandler<HtmlField>
{
    public override Task BuildIndexAsync(HtmlField field, BuildFieldIndexContext context)
    {
        var options = context.Settings.ToOptions();

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, field.Html, options);
        }

        return Task.CompletedTask;
    }
}
