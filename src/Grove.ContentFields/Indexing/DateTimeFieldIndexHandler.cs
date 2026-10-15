using Grove.ContentFields.Fields;
using Grove.Indexing;

namespace Grove.ContentFields.Indexing;

public class DateTimeFieldIndexHandler : ContentFieldIndexHandler<DateTimeField>
{
    public override Task BuildIndexAsync(DateTimeField field, BuildFieldIndexContext context)
    {
        var options = context.Settings.ToOptions();

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, field.Value, options);
        }

        return Task.CompletedTask;
    }
}
