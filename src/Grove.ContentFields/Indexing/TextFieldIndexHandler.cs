using Grove.ContentFields.Fields;
using Grove.Indexing;

namespace Grove.ContentFields.Indexing;

public class TextFieldIndexHandler : ContentFieldIndexHandler<TextField>
{
    public override Task BuildIndexAsync(TextField field, BuildFieldIndexContext context)
    {
        var options = context.Settings.ToOptions();

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, field.Text, options);
        }

        return Task.CompletedTask;
    }
}
