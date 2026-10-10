using Ganss.Xss;

namespace Crest.Infrastructure.Html;

public class HtmlSanitizerOptions
{
    public List<Action<HtmlSanitizer>> Configure { get; } = [];
}
