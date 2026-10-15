using Grove.Data.Protocols.Html.Models;
using Grove.Data.Indexing;

namespace Grove.Data.Protocols.Html.Indexing;

public class HtmlBodyPartIndexHandler : ContentPartIndexHandler<HtmlBodyPart>
{
    public override Task BuildIndexAsync(HtmlBodyPart part, BuildPartIndexContext context)
    {
        var options = context.Settings.ToOptions() | DocumentIndexOptions.Sanitize;

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, part.Html, options);
        }

        return Task.CompletedTask;
    }
}
