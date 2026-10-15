using Markdig;

namespace Grove.Markdown.Services;

public class MarkdownPipelineOptions
{
    public List<Action<MarkdownPipelineBuilder>> Configure { get; } = [];
}
