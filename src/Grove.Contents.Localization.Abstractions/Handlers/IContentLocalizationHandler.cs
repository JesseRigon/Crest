namespace Grove.Contents.Localization.Handlers;

public interface IContentLocalizationHandler
{
    Task LocalizingAsync(LocalizationContentContext context);
    Task LocalizedAsync(LocalizationContentContext context);
}
