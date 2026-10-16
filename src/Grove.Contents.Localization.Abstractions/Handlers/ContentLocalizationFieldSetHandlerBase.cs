using Grove.Contents.Management;

namespace Grove.Contents.Localization.Handlers;

public abstract class ContentLocalizationFieldSetHandlerBase<TFieldSet> : IContentLocalizationFieldSetHandler where TFieldSet : ContentFieldSet, new()
{
    async Task IContentLocalizationFieldSetHandler.LocalizingAsync(LocalizationContentContext context, ContentFieldSet part)
    {
        if (part is TFieldSet tpart)
        {
            await LocalizingAsync(context, tpart);
        }
    }

    async Task IContentLocalizationFieldSetHandler.LocalizedAsync(LocalizationContentContext context, ContentFieldSet part)
    {
        if (part is TFieldSet tpart)
        {
            await LocalizedAsync(context, tpart);
        }
    }
    public virtual Task LocalizingAsync(LocalizationContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task LocalizedAsync(LocalizationContentContext context, TFieldSet part) => Task.CompletedTask;
}
