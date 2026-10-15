using Markdig;

namespace Grove.Contents.Markdown.Services;

public class MarkdownPipelineOptions
{
    public List<Action<MarkdownPipelineBuilder>> Configure { get; } = [];
}
