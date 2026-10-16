using Grove.Data.Indexing;
using Grove.Contents.Markdown.Models;

namespace Grove.Contents.Markdown.Indexing;

public class MarkdownBodyFieldSetIndexHandler : ContentFieldSetIndexHandler<MarkdownBodyFieldSet>
{
    public override Task BuildIndexAsync(MarkdownBodyFieldSet part, BuildFieldSetIndexContext context)
    {
        var options = context.Settings.ToOptions() | DocumentIndexOptions.Sanitize;

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, part.Markdown, options);
        }

        return Task.CompletedTask;
    }
}
