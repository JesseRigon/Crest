namespace Grove.Contents.Management.Handlers;

public abstract class ContentFieldSetHandler<TFieldSet> : IContentFieldSetHandler where TFieldSet : ContentFieldSet, new()
{
    Task IContentFieldSetHandler.ActivatedAsync(ActivatedContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? ActivatedAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.ActivatingAsync(ActivatingContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? ActivatingAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.InitializingAsync(InitializingContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? InitializingAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.InitializedAsync(InitializingContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? InitializedAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.CreatingAsync(CreateContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? CreatingAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.CreatedAsync(CreateContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? CreatedAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.LoadingAsync(LoadContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? LoadingAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.LoadedAsync(LoadContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? LoadedAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.ImportingAsync(ImportContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? ImportingAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.ImportedAsync(ImportContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? ImportedAsync(context, tpart)
            : Task.CompletedTask;
    }
    Task IContentFieldSetHandler.UpdatingAsync(UpdateContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? UpdatingAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.UpdatedAsync(UpdateContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? UpdatedAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.ValidatingAsync(ValidateContentContext context, ContentFieldSet part)
    {
        if (part is TFieldSet tpart)
        {
            if (context is ValidateContentFieldSetContext partContext)
            {
                return ValidatingAsync(partContext, tpart);
            }

            return ValidatingAsync(context, tpart);
        }

        return Task.CompletedTask;
    }

    Task IContentFieldSetHandler.ValidatedAsync(ValidateContentContext context, ContentFieldSet part)
    {
        if (part is TFieldSet tpart)
        {
            if (context is ValidateContentFieldSetContext partContext)
            {
                return ValidatedAsync(partContext, tpart);
            }

            return ValidatedAsync(context, tpart);
        }

        return Task.CompletedTask;
    }

    Task IContentFieldSetHandler.VersioningAsync(VersionContentContext context, ContentFieldSet existing, ContentFieldSet building)
    {
        return existing is TFieldSet texisting && building is TFieldSet tbuilding
            ? VersioningAsync(context, texisting, tbuilding)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.VersionedAsync(VersionContentContext context, ContentFieldSet existing, ContentFieldSet building)
    {
        return existing is TFieldSet texisting && building is TFieldSet tbuilding
            ? VersionedAsync(context, texisting, tbuilding)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.DraftSavingAsync(SaveDraftContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? DraftSavingAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.DraftSavedAsync(SaveDraftContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? DraftSavedAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.PublishingAsync(PublishContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? PublishingAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.PublishedAsync(PublishContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? PublishedAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.UnpublishingAsync(PublishContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? UnpublishingAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.UnpublishedAsync(PublishContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? UnpublishedAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.RemovingAsync(RemoveContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? RemovingAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.RemovedAsync(RemoveContentContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? RemovedAsync(context, tpart)
            : Task.CompletedTask;
    }

    Task IContentFieldSetHandler.GetContentItemAspectAsync(ContentItemAspectContext context, ContentFieldSet part)
    {
        return part is TFieldSet tpart
            ? GetContentItemAspectAsync(context, tpart)
            : Task.CompletedTask;
    }
    async Task IContentFieldSetHandler.CloningAsync(CloneContentContext context, ContentFieldSet part)
    {
        if (part is TFieldSet tpart)
        {
            await CloningAsync(context, tpart);
        }
    }

    async Task IContentFieldSetHandler.ClonedAsync(CloneContentContext context, ContentFieldSet part)
    {
        if (part is TFieldSet tpart)
        {
            await ClonedAsync(context, tpart);
        }
    }

    public virtual Task ActivatedAsync(ActivatedContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task ActivatingAsync(ActivatingContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task InitializingAsync(InitializingContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task InitializedAsync(InitializingContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task CreatingAsync(CreateContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task CreatedAsync(CreateContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task LoadingAsync(LoadContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task LoadedAsync(LoadContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task ImportingAsync(ImportContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task ImportedAsync(ImportContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task UpdatingAsync(UpdateContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task UpdatedAsync(UpdateContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task ValidatingAsync(ValidateContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task ValidatedAsync(ValidateContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task VersioningAsync(VersionContentContext context, TFieldSet existing, TFieldSet building) => Task.CompletedTask;
    public virtual Task VersionedAsync(VersionContentContext context, TFieldSet existing, TFieldSet building) => Task.CompletedTask;
    public virtual Task DraftSavingAsync(SaveDraftContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task DraftSavedAsync(SaveDraftContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task PublishingAsync(PublishContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task PublishedAsync(PublishContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task UnpublishingAsync(PublishContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task UnpublishedAsync(PublishContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task RemovingAsync(RemoveContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task RemovedAsync(RemoveContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task GetContentItemAspectAsync(ContentItemAspectContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task CloningAsync(CloneContentContext context, TFieldSet part) => Task.CompletedTask;
    public virtual Task ClonedAsync(CloneContentContext context, TFieldSet part) => Task.CompletedTask;

    protected virtual Task ValidatingAsync(ValidateContentFieldSetContext context, TFieldSet part)
        => ValidatingAsync(context as ValidateContentContext, part);

    protected virtual Task ValidatedAsync(ValidateContentFieldSetContext context, TFieldSet part)
        => ValidatedAsync(context as ValidateContentContext, part);
}
