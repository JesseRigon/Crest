using Markdig;

namespace Crest.Markdown.Services;

public class MarkdownPipelineOptions
{
    public List<Action<MarkdownPipelineBuilder>> Configure { get; } = [];
}
