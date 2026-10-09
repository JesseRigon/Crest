using Crest.Indexing;
using Crest.Markdown.Models;

namespace Crest.Markdown.Indexing;

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
