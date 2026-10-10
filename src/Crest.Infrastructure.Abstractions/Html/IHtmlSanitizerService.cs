namespace Crest.Infrastructure.Html;

public interface IHtmlSanitizerService
{
    string Sanitize(string html);
}
