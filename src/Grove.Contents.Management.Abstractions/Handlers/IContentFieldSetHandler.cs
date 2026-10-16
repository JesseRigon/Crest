namespace Grove.Contents.Management.Handlers;

/// <summary>
/// An implementation of this class is called for all the parts of a content item.
/// </summary>
public interface IContentFieldSetHandler
{
    Task ActivatedAsync(ActivatedContentContext context, ContentFieldSet part);
    Task ActivatingAsync(ActivatingContentContext context, ContentFieldSet part);
    Task InitializingAsync(InitializingContentContext context, ContentFieldSet part);
    Task InitializedAsync(InitializingContentContext context, ContentFieldSet part);
    Task CreatingAsync(CreateContentContext context, ContentFieldSet part);
    Task CreatedAsync(CreateContentContext context, ContentFieldSet part);
    Task LoadingAsync(LoadContentContext context, ContentFieldSet part);
    Task LoadedAsync(LoadContentContext context, ContentFieldSet part);
    Task ImportingAsync(ImportContentContext context, ContentFieldSet part);
    Task ImportedAsync(ImportContentContext context, ContentFieldSet part);
    Task UpdatingAsync(UpdateContentContext context, ContentFieldSet part);
    Task UpdatedAsync(UpdateContentContext context, ContentFieldSet part);
    Task ValidatingAsync(ValidateContentContext context, ContentFieldSet part);
    Task ValidatedAsync(ValidateContentContext context, ContentFieldSet part);
    Task VersioningAsync(VersionContentContext context, ContentFieldSet existing, ContentFieldSet building);
    Task VersionedAsync(VersionContentContext context, ContentFieldSet existing, ContentFieldSet building);
    Task DraftSavingAsync(SaveDraftContentContext context, ContentFieldSet part);
    Task DraftSavedAsync(SaveDraftContentContext context, ContentFieldSet part);
    Task PublishingAsync(PublishContentContext context, ContentFieldSet part);
    Task PublishedAsync(PublishContentContext context, ContentFieldSet part);
    Task UnpublishingAsync(PublishContentContext context, ContentFieldSet part);
    Task UnpublishedAsync(PublishContentContext context, ContentFieldSet part);
    Task RemovingAsync(RemoveContentContext context, ContentFieldSet part);
    Task RemovedAsync(RemoveContentContext context, ContentFieldSet part);
    Task GetContentItemAspectAsync(ContentItemAspectContext context, ContentFieldSet part);
    Task CloningAsync(CloneContentContext context, ContentFieldSet part);
    Task ClonedAsync(CloneContentContext context, ContentFieldSet part);
}
