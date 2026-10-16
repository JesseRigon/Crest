namespace Grove.Contents.Types.Events;

public interface IContentDefinitionEventHandler
{
    void ContentTypeCreated(ContentTypeCreatedContext context);

    void ContentTypeUpdated(ContentTypeUpdatedContext context);

    void ContentTypeRemoved(ContentTypeRemovedContext context);

    void ContentTypeImporting(ContentTypeImportingContext context);

    void ContentTypeImported(ContentTypeImportedContext context);

    void ContentFieldSetCreated(ContentFieldSetCreatedContext context);

    void ContentFieldSetUpdated(ContentFieldSetUpdatedContext context);

    void ContentFieldSetRemoved(ContentFieldSetRemovedContext context);

    void ContentFieldSetAttached(ContentFieldSetAttachedContext context);

    void ContentFieldSetDetached(ContentFieldSetDetachedContext context);

    void ContentFieldSetImporting(ContentFieldSetImportingContext context);

    void ContentFieldSetImported(ContentFieldSetImportedContext context);

    void ContentTypeFieldSetUpdated(ContentTypeFieldSetUpdatedContext context);

    void ContentFieldAttached(ContentFieldAttachedContext context);

    void ContentFieldDetached(ContentFieldDetachedContext context);

    void ContentFieldUpdated(ContentFieldUpdatedContext context);
}
