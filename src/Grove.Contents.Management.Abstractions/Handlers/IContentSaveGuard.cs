namespace Grove.Contents.Management.Handlers;

/// <summary>
/// A rule the content manager runs on every create and update, after the creating or
/// updating handlers and before the item is written. Unlike a content handler, whose
/// exceptions are logged and swallowed, a guard's refusal stops the write: the content
/// manager throws <see cref="ContentSaveRefusedException"/>. This is where a rule that
/// generic editors must not bypass belongs (docs/content-engine.md › Relationship guard).
/// </summary>
public interface IContentSaveGuard
{
    Task GuardAsync(ContentSaveGuardContext context);
}

/// <summary>What a save guard sees: the item about to be written and the content manager
/// writing it (for aspects such as the contained items; guards never save through it).</summary>
public sealed class ContentSaveGuardContext(ContentItem contentItem, IContentManager contentManager)
{
    private readonly List<string> _errors = [];

    public ContentItem ContentItem { get; } = contentItem;

    public IContentManager ContentManager { get; } = contentManager;

    public IReadOnlyList<string> Errors => _errors;

    public void Fail(string error) => _errors.Add(error);
}

/// <summary>A save refused by one or more <see cref="IContentSaveGuard"/>s. An
/// <see cref="InvalidOperationException"/>, so API layers that report caller errors as
/// 400s report this one the same way.</summary>
public sealed class ContentSaveRefusedException(IReadOnlyList<string> errors)
    : InvalidOperationException(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
