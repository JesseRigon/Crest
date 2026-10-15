using Grove.Contents.Fields.Fields;
using Grove.Data.Indexing;

namespace Grove.Contents.Fields.Indexing;

public class TimeFieldIndexHandler : ContentFieldIndexHandler<TimeField>
{
    public override Task BuildIndexAsync(TimeField field, BuildFieldIndexContext context)
    {
        var options = context.Settings.ToOptions();

        DateTime? indexedValue = null;
        if (field.Value.HasValue)
        {
            indexedValue = new DateTime(field.Value.Value.Ticks);
        }

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, indexedValue, options);
        }

        return Task.CompletedTask;
    }
}
