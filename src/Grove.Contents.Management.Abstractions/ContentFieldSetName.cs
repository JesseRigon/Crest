namespace Grove.Contents.Management;

/// <summary>
/// Derives the stored name of a content element type: the key it is stored under in a content
/// item and, for a field set, the name of its definition. A field set class is stored without
/// its "FieldSet" suffix (<c>TitleFieldSet</c> is stored as <c>Title</c>); any other type is
/// stored under its class name.
/// </summary>
public static class ContentFieldSetName
{
    private const string Suffix = "FieldSet";

    public static string Of<TElement>()
        where TElement : ContentElement
        => Cache<TElement>.Name;

    public static string Of(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var name = type.Name;

        return type.IsSubclassOf(typeof(ContentFieldSet))
            && name.Length > Suffix.Length
            && name.EndsWith(Suffix, StringComparison.Ordinal)
                ? name[..^Suffix.Length]
                : name;
    }

    private static class Cache<TElement>
    {
        public static readonly string Name = Of(typeof(TElement));
    }
}
