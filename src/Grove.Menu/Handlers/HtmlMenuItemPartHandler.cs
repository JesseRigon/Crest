using Grove.ContentManagement;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata;
using Grove.Infrastructure.Html;
using Grove.Menu.Models;
using Grove.Menu.Settings;

namespace Grove.Menu.Handlers;

[Obsolete("This handler is no longer used.")]
public class HtmlMenuItemPartHandler : ContentPartHandler<HtmlMenuItemPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    public HtmlMenuItemPartHandler(IContentDefinitionManager contentDefinitionManager, IHtmlSanitizerService htmlSanitizerService)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _htmlSanitizerService = htmlSanitizerService;
    }
    
    public override async Task ImportedAsync(ImportContentContext context, HtmlMenuItemPart part)
    {
        var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.ContentItem.ContentType);

        if (typeDefinition.GetSettings<HtmlMenuItemPartSettings>() is { SanitizeHtml: true })
        {
            context.ContentItem.Alter<HtmlMenuItemPart>(part => 
                part.Html = _htmlSanitizerService.Sanitize(part.Html));
        }
    }
}
