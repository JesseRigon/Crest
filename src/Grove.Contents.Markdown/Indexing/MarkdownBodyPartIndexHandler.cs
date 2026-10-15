using Grove.Data.Indexing;
using Grove.Contents.Markdown.Models;

namespace Grove.Contents.Markdown.Indexing;

public class MarkdownBodyPartIndexHandler : ContentPartIndexHandler<MarkdownBodyPart>
{
    public override Task BuildIndexAsync(MarkdownBodyPart part, BuildPartIndexContext context)
    {
        var options = context.Settings.ToOptions() | DocumentIndexOptions.Sanitize;

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, part.Markdown, options);
        }

        return Task.CompletedTask;
    }
}
