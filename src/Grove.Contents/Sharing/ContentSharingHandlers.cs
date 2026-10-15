using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Types.Events;
using Grove.Environment.Shell.Scope;
using Grove.Access.Sharing;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Contents.Sharing;

/// <summary>
/// Keeps content items' access index current (docs/sharing.md, requirement 78): an item is
/// recalculated when it is created, updated (its owner may have changed), published or
/// unpublished, and removed from the index when no version is left. An item never calculated is
/// in no list.
/// </summary>
public sealed class ContentSharingHandler(IEnumerable<IShareableKind> kinds, IShareAccessIndex index) : ContentHandlerBase
{
    public override Task CreatedAsync(CreateContentContext context) => RecalculateAsync(context.ContentItem);

    public override Task UpdatedAsync(UpdateContentContext context) => RecalculateAsync(context.ContentItem);

    public override Task PublishedAsync(PublishContentContext context) => RecalculateAsync(context.ContentItem);

    public override Task UnpublishedAsync(PublishContentContext context) => RecalculateAsync(context.ContentItem);

    public override Task ImportedAsync(ImportContentContext context) => RecalculateAsync(context.ContentItem);

    public override Task RemovedAsync(RemoveContentContext context) =>
        context.NoActiveVersionLeft
            ? index.RemoveAsync(new ShareableRef(ContentShareableKind.KindName, context.ContentItem.ContentItemId))
            : RecalculateAsync(context.ContentItem);

    private Task RecalculateAsync(ContentItem item)
    {
        if (string.IsNullOrEmpty(item.ContentItemId))
        {
            return Task.CompletedTask;
        }

        kinds.OfType<ContentShareableKind>().FirstOrDefault()?.Remember(item);
        return index.RecalculateAsync(new ShareableRef(ContentShareableKind.KindName, item.ContentItemId));
    }
}

/// <summary>
/// A content type created, changed or removed: the type's own index document follows, and a
/// change recalculates its items (their chain runs through the type). The definition events are
/// raised synchronously from inside the definition update, so the work runs after it.
/// </summary>
public sealed class ContentTypeSharingHandler : IContentDefinitionEventHandler
{
    public void ContentTypeCreated(ContentTypeCreatedContext context) => Defer(context.ContentTypeDefinition?.Name, includeDescendants: false, remove: false);

    public void ContentTypeUpdated(ContentTypeUpdatedContext context) => Defer(context.ContentTypeDefinition?.Name, includeDescendants: true, remove: false);

    public void ContentTypeRemoved(ContentTypeRemovedContext context) => Defer(context.ContentTypeDefinition?.Name, includeDescendants: false, remove: true);

    private static void Defer(string? type, bool includeDescendants, bool remove)
    {
        if (string.IsNullOrEmpty(type))
        {
            return;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var index = scope.ServiceProvider.GetRequiredService<IShareAccessIndex>();
            var target = new ShareableRef(ContentTypeShareableKind.KindName, type);
            await (remove ? index.RemoveAsync(target) : index.RecalculateAsync(target, includeDescendants));
        });
    }

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
