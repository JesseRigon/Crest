using System.Collections.Concurrent;

namespace Grove.Contents.Management.Display.ContentDisplay;

/// <summary>
/// Provides cached alternate patterns for ContentFieldSet shapes in ContentItemDisplayCoordinator.
/// Alternates are computed once per unique configuration and cached for reuse.
/// </summary>
internal static class ContentFieldSetShapeAlternatesFactory
{
    private static readonly ConcurrentDictionary<DisplayAlternatesCacheKey, string[]> s_displayCache = new();
    private static readonly ConcurrentDictionary<EditorAlternatesCacheKey, string[]> s_editorCache = new();

    /// <summary>
    /// Gets or creates cached alternates for a ContentFieldSet display shape.
    /// </summary>
    public static string[] GetDisplayAlternates(
        string contentType,
        string fieldSetTypeName,
        string fieldSetName,
        string stereotype,
        bool hasStereotype,
        string displayType)
    {
        var key = new DisplayAlternatesCacheKey(
            contentType,
            fieldSetTypeName,
            fieldSetName,
            stereotype ?? string.Empty,
            hasStereotype,
            displayType);

        return s_displayCache.GetOrAdd(key, BuildDisplayAlternates);
    }

    /// <summary>
    /// Gets or creates cached alternates for a ContentFieldSet_Edit shape.
    /// </summary>
    public static string[] GetEditorAlternates(
        string contentType,
        string fieldSetTypeName,
        string fieldSetName,
        bool isNamedFieldSet)
    {
        var key = new EditorAlternatesCacheKey(
            contentType,
            fieldSetTypeName,
            fieldSetName,
            isNamedFieldSet);

        return s_editorCache.GetOrAdd(key, BuildEditorAlternates);
    }

    private static string[] BuildDisplayAlternates(DisplayAlternatesCacheKey key)
    {
        var alternates = new List<string>();
        var displayTypes = new[] { string.Empty, "_" + key.DisplayType };

        foreach (var displayType in displayTypes)
        {
            // eg. ServicePart,  ServicePart.Summary
            alternates.Add($"{key.FieldSetTypeName}{displayType}");

            // [ContentType]_[DisplayType]__[FieldSetType]
            // e.g. LandingPage-ServicePart, LandingPage-ServicePart.Summary
            alternates.Add($"{key.ContentType}{displayType}__{key.FieldSetTypeName}");

            if (key.HasStereotype)
            {
                // [Stereotype]_[DisplayType]__[FieldSetType],
                // e.g. Widget-ServicePart
                alternates.Add($"{key.Stereotype}{displayType}__{key.FieldSetTypeName}");
            }
        }

        if (key.FieldSetTypeName != key.FieldSetName)
        {
            foreach (var displayType in displayTypes)
            {
                // [ContentType]_[DisplayType]__[FieldSetName]
                // e.g. Employee-Address1, Employee-Address2
                alternates.Add($"{key.ContentType}{displayType}__{key.FieldSetName}");

                if (key.HasStereotype)
                {
                    // [Stereotype]_[DisplayType]__[FieldSetType]__[FieldSetName]
                    // e.g. Widget-Services
                    alternates.Add($"{key.Stereotype}{displayType}__{key.FieldSetTypeName}__{key.FieldSetName}");
                }
            }
        }

        return alternates.ToArray();
    }

    private static string[] BuildEditorAlternates(EditorAlternatesCacheKey key)
    {
        const string shapeType = "ContentFieldSet_Edit";

        var alternates = new List<string>
        {
            // ContentFieldSet_Edit__[FieldSetType]
            // eg ContentFieldSet-ServicePart.Edit
            $"{shapeType}__{key.FieldSetTypeName}",

            // ContentFieldSet_Edit__[ContentType]__[FieldSetType]
            // e.g. ContentFieldSet-LandingPage-ServicePart.Edit
            $"{shapeType}__{key.ContentType}__{key.FieldSetTypeName}",
        };

        if (key.IsNamedFieldSet)
        {
            // ContentFieldSet_Edit__[ContentType]__[FieldSetName]
            // e.g. ContentFieldSet-LandingPage-BillingService.Edit ContentFieldSet-LandingPage-HelplineService.Edit
            alternates.Add($"{shapeType}__{key.ContentType}__{key.FieldSetName}");
        }

        return alternates.ToArray();
    }

    internal readonly record struct DisplayAlternatesCacheKey(
        string ContentType,
        string FieldSetTypeName,
        string FieldSetName,
        string Stereotype,
        bool HasStereotype,
        string DisplayType);

    internal readonly record struct EditorAlternatesCacheKey(
        string ContentType,
        string FieldSetTypeName,
        string FieldSetName,
        bool IsNamedFieldSet);
}
