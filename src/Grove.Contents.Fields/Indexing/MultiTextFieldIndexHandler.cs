using Grove.Contents.Fields.Fields;
using Grove.Data.Indexing;

namespace Grove.Contents.Fields.Indexing;

public class MultiTextFieldIndexHandler : ContentFieldIndexHandler<MultiTextField>
{
    public override Task BuildIndexAsync(MultiTextField field, BuildFieldIndexContext context)
    {
        var options = context.Settings.ToOptions();

        foreach (var key in context.Keys)
        {
            foreach (var value in field.Values)
            {
                context.DocumentIndex.Set(key, value, options);
            }
        }

        return Task.CompletedTask;
    }
}
