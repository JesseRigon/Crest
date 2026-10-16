namespace Grove.Contents.Management;

/// <summary>
/// Adds an item to a referenceable embedding by name, for an option list field that allows it
/// (a tag added to a taxonomy's <c>Terms</c>): the module that owns the embedding decides the
/// item's type, places it, authorizes the caller and saves the parent.
/// </summary>
public interface IEmbeddedContentItemFactory
{
    /// <summary>The type that declares the embedding (<c>Taxonomy</c>).</summary>
    string ContentType { get; }

    /// <summary>The embedding's name (<c>Terms</c>).</summary>
    string Embedding { get; }

    /// <summary>Adds an item named <paramref name="displayText"/> to <paramref name="parentContentItemId"/>'s embedding.</summary>
    Task<EmbeddedContentItemCreation> CreateAsync(string parentContentItemId, string displayText, CancellationToken cancellationToken = default);
}

/// <summary>The added item, or why it was not added (<see cref="Forbidden"/> when the caller may not add items).</summary>
public sealed record EmbeddedContentItemCreation(ContentItem Item, IReadOnlyList<string> Errors, bool Forbidden = false);
