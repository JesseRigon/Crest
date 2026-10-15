namespace Grove.Contents.Localization.Handlers;

public class ContentLocalizationHandlerBase : IContentLocalizationHandler
{
    public virtual Task LocalizedAsync(LocalizationContentContext context)
    {
        return Task.CompletedTask;
    }
    public virtual Task LocalizingAsync(LocalizationContentContext context)
    {
        return Task.CompletedTask;
    }
}
