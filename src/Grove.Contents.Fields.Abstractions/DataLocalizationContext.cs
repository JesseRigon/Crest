namespace Grove.Contents.Fields;

/// <summary>
/// The data localization contexts of content definition labels (docs/content-engine.md ›
/// Localization): every name a definition gives is a localization source, keyed by where it is
/// shown. Type names are under <c>Grove.Contents.Types.DataLocalizationContext.ContentType</c>.
/// </summary>
public static class DataLocalizationContext
{
    public const char Separator = ':';

    /// <summary>The context of a field's display name and description: "Content Fields:{FieldSetAttachment}",
    /// the name the field set is attached under on the type.</summary>
    public static string ContentField(string fieldSetAttachment)
        => $"Content Fields{Separator}{fieldSetAttachment}";

    /// <summary>The context of field sets' display names and descriptions (the Field groups page).</summary>
    public const string ContentFieldSet = "Content Field Sets";

    /// <summary>The context of a type's field-set attachment names ("Billing", "Shipping"): "Content Field Set Attachments:{Type}".</summary>
    public static string ContentFieldSetAttachment(string contentType)
        => $"Content Field Set Attachments{Separator}{contentType}";

    /// <summary>The context of a type's embedding names: "Content Embeddings:{Type}".</summary>
    public static string ContentEmbedding(string contentType)
        => $"Content Embeddings{Separator}{contentType}";
}
