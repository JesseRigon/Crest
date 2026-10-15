using Ganss.Xss;

namespace Grove.Infrastructure.Html;

public class HtmlSanitizerOptions
{
    public List<Action<HtmlSanitizer>> Configure { get; } = [];
}
