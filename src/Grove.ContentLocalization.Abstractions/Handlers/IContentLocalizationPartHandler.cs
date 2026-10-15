using Grove.ContentManagement;

namespace Grove.ContentLocalization.Handlers;

public interface IContentLocalizationPartHandler
{
    Task LocalizingAsync(LocalizationContentContext context, ContentPart part);
    Task LocalizedAsync(LocalizationContentContext context, ContentPart part);
}
