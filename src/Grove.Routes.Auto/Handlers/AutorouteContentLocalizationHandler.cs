using Grove.Routes.Auto.Models;
using Grove.Contents.Management;

namespace Grove.Contents.Localization.Handlers;

internal sealed class AutorouteContentLocalizationHandler : IContentLocalizationHandler
{
    public Task LocalizedAsync(LocalizationContentContext context) => Task.CompletedTask;

    public Task LocalizingAsync(LocalizationContentContext context)
    {
        if (context.ContentItem.Has<AutoroutePart>())
        {
            // Clearing the AutoroutePart path to regenerate the permalink automatically.
            context.ContentItem.Alter<AutoroutePart>(p => p.Path = null);
        }

        return Task.CompletedTask;
    }
}
