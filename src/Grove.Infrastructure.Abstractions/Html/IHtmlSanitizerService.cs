namespace Grove.Infrastructure.Html;

public interface IHtmlSanitizerService
{
    string Sanitize(string html);
}
