using Grove.Contents.Management;

namespace Grove.Contents.Localization.Handlers;

public interface IContentLocalizationPartHandler
{
    Task LocalizingAsync(LocalizationContentContext context, ContentPart part);
    Task LocalizedAsync(LocalizationContentContext context, ContentPart part);
}
