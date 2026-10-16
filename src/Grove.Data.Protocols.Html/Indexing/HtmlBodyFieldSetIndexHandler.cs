using Grove.Data.Protocols.Html.Models;
using Grove.Data.Indexing;

namespace Grove.Data.Protocols.Html.Indexing;

public class HtmlBodyFieldSetIndexHandler : ContentFieldSetIndexHandler<HtmlBodyFieldSet>
{
    public override Task BuildIndexAsync(HtmlBodyFieldSet part, BuildFieldSetIndexContext context)
    {
        var options = context.Settings.ToOptions() | DocumentIndexOptions.Sanitize;

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, part.Html, options);
        }

        return Task.CompletedTask;
    }
}
