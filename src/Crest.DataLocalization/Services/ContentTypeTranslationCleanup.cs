using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement.Metadata;
using Crest.ContentTypes.Events;
using Crest.Environment.Shell.Scope;

namespace Crest.DataLocalization.Services;

/// <summary>
/// Deletes a content type's display-name translations when the type itself is deleted, so they
/// do not linger as orphans the Translations editor cannot show.
/// </summary>
/// <remarks>
/// Translations are keyed on the display name, and display names are not unique: if another
/// type still bears the same one, its translation stays. The removal runs as a deferred shell
/// scope task because the event is raised synchronously from inside the content definition
/// update, and the store write belongs after it, not inside it.
/// </remarks>
public sealed class ContentTypeTranslationCleanup : IContentDefinitionEventHandler
{
    private const string ContentTypesContext = Crest.ContentTypes.DataLocalizationContext.ContentType;

    public void ContentTypeRemoved(ContentTypeRemovedContext context)
    {
        var displayName = context.ContentTypeDefinition?.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var definitionManager = scope.ServiceProvider.GetRequiredService<IContentDefinitionManager>();
            var stillUsed = (await definitionManager.ListTypeDefinitionsAsync())
                .Any(type => string.Equals(type.DisplayName, displayName, StringComparison.Ordinal));
            if (stillUsed)
            {
                return;
            }

            var translations = scope.ServiceProvider.GetRequiredService<ITranslationsManager>();
            await translations.RemoveTranslationsAsync(ContentTypesContext, [displayName]);
        });
    }

    public void ContentTypeCreated(ContentTypeCreatedContext context) { }
    public void ContentTypeUpdated(ContentTypeUpdatedContext context) { }
    public void ContentTypeImporting(ContentTypeImportingContext context) { }
    public void ContentTypeImported(ContentTypeImportedContext context) { }
    public void ContentPartCreated(ContentPartCreatedContext context) { }
    public void ContentPartUpdated(ContentPartUpdatedContext context) { }
    public void ContentPartRemoved(ContentPartRemovedContext context) { }
    public void ContentPartAttached(ContentPartAttachedContext context) { }
    public void ContentPartDetached(ContentPartDetachedContext context) { }
    public void ContentPartImporting(ContentPartImportingContext context) { }
    public void ContentPartImported(ContentPartImportedContext context) { }
    public void ContentTypePartUpdated(ContentTypePartUpdatedContext context) { }
    public void ContentFieldAttached(ContentFieldAttachedContext context) { }
    public void ContentFieldUpdated(ContentFieldUpdatedContext context) { }
    public void ContentFieldDetached(ContentFieldDetachedContext context) { }
    public void ContentPartFieldUpdated(ContentPartFieldUpdatedContext context) { }
}
