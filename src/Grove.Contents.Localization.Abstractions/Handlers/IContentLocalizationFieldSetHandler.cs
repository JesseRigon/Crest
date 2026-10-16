using Grove.Contents.Management;

namespace Grove.Contents.Localization.Handlers;

public interface IContentLocalizationFieldSetHandler
{
    Task LocalizingAsync(LocalizationContentContext context, ContentFieldSet part);
    Task LocalizedAsync(LocalizationContentContext context, ContentFieldSet part);
}
