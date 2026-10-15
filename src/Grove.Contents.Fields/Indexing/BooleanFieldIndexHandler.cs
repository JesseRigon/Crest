using Grove.Contents.Fields.Fields;
using Grove.Data.Indexing;

namespace Grove.Contents.Fields.Indexing;

public class BooleanFieldIndexHandler : ContentFieldIndexHandler<BooleanField>
{
    public override Task BuildIndexAsync(BooleanField field, BuildFieldIndexContext context)
    {
        var options = context.Settings.ToOptions();

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, field.Value, options);
        }

        return Task.CompletedTask;
    }
}
