using System.Collections.Concurrent;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.Display.ContentDisplay;

/// <summary>
/// Provides cached alternate patterns for field sets.
/// Alternates are computed once per unique part configuration and cached for reuse.
/// </summary>
public static class ContentFieldSetAlternatesFactory
{
    private const string DisplayToken = "_Display";

    private static readonly ConcurrentDictionary<FieldSetAlternatesCacheKey, string[]> s_cache = new();

    /// <summary>
    /// Gets or creates cached alternates for a field set configuration.
    /// </summary>
    /// <param name="definition">The content type part definition.</param>
    /// <param name="shapeType">The shape type being rendered.</param>
    /// <returns>A cached array of alternates.</returns>
    public static string[] GetAlternates(ContentTypeFieldSetDefinition definition, string shapeType, string displayType)
    {
        var key = new FieldSetAlternatesCacheKey(
            definition.ContentTypeDefinition.Name,
            definition.FieldSetDefinition.Name,
            definition.Name,
            definition.Editor() ?? string.Empty,
            definition.DisplayMode() ?? string.Empty,
            definition.ContentTypeDefinition.GetStereotype() ?? string.Empty,
            shapeType,
            displayType ?? string.Empty);

        return s_cache.GetOrAdd(key, BuildAlternates);
    }

    /// <summary>
    /// Cache key for part alternates, using a readonly record struct for efficient hashing and equality.
    /// </summary>
    internal readonly record struct FieldSetAlternatesCacheKey(
        string ContentType,
        string FieldSetType,
        string FieldSetName,
        string Editor,
        string DisplayMode,
        string Stereotype,
        string ShapeType,
        string DisplayType);

    private static string[] BuildAlternates(FieldSetAlternatesCacheKey key)
    {
        var alternates = new List<string>();
        var editorFieldSetType = !string.IsNullOrEmpty(key.Editor)
            ? key.FieldSetType + "_Edit__" + key.Editor
            : key.FieldSetType + "_Edit";
        var hasDisplayMode = !string.IsNullOrEmpty(key.DisplayMode);
        var isDisplayModeShapeType = key.ShapeType == key.FieldSetType + "_Display__" + key.DisplayMode;
        var isFieldSetTypeShape = key.ShapeType == key.FieldSetType;
        var isEditorShape = key.ShapeType == editorFieldSetType;
        var hasStereotype = !string.IsNullOrEmpty(key.Stereotype);
        var fieldSetTypeEqualsFieldSetName = key.FieldSetType == key.FieldSetName;

        string[] displayTypes;

        if (isEditorShape)
        {
            displayTypes = ["_" + key.DisplayType];
        }
        else
        {
            displayTypes = ["", "_" + key.DisplayType];

            if (!isDisplayModeShapeType)
            {
                // [ShapeType]_[DisplayType], e.g. HtmlBodyFieldSet.Summary, Taxonomy.Summary, ChildContentItemsFieldSetFeed.Summary
                alternates.Add($"{key.ShapeType}_{key.DisplayType}");
            }
        }

        if (isFieldSetTypeShape || isEditorShape)
        {
            foreach (var dt in displayTypes)
            {
                // [ContentType]_[DisplayType]__[FieldSetType], e.g. Blog-HtmlBodyFieldSet, LandingPage-Taxonomy
                alternates.Add($"{key.ContentType}{dt}__{key.FieldSetType}");

                if (hasStereotype)
                {
                    // [Stereotype]__[DisplayType]__[FieldSetType], e.g. Widget-ContentsMetadata
                    alternates.Add($"{key.Stereotype}{dt}__{key.FieldSetType}");
                }
            }

            if (!fieldSetTypeEqualsFieldSetName)
            {
                foreach (var dt in displayTypes)
                {
                    // [ContentType]_[DisplayType]__[FieldSetName], e.g. LandingPage-Services
                    alternates.Add($"{key.ContentType}{dt}__{key.FieldSetName}");

                    if (hasStereotype)
                    {
                        // [Stereotype]_[DisplayType]__[FieldSetType]__[FieldSetName], e.g. Widget-ServicePart-Services
                        alternates.Add($"{key.Stereotype}{dt}__{key.FieldSetType}__{key.FieldSetName}");
                    }
                }
            }
        }
        else
        {
            if (hasDisplayMode)
            {
                // [FieldSetType]_[DisplayType]__[DisplayMode]_Display, e.g. HtmlBodyFieldSet-MyDisplayMode.Display.Summary
                alternates.Add($"{key.FieldSetType}_{key.DisplayType}__{key.DisplayMode}{DisplayToken}");
            }

            var lastAlternatesOfNamedFieldSet = new List<string>();

            foreach (var dt in displayTypes)
            {
                var shapeTypeSuffix = key.ShapeType;
                var displayTypeDisplayToken = dt;

                if (hasDisplayMode)
                {
                    if (isDisplayModeShapeType)
                    {
                        shapeTypeSuffix = key.DisplayMode;
                    }
                    else
                    {
                        shapeTypeSuffix = $"{shapeTypeSuffix}__{key.DisplayMode}";
                    }

                    if (dt == "")
                    {
                        displayTypeDisplayToken = DisplayToken;
                    }
                    else
                    {
                        shapeTypeSuffix = $"{shapeTypeSuffix}{DisplayToken}";
                    }
                }

                // [ContentType]_[DisplayType]__[FieldSetType]__[ShapeType], e.g. Blog-ChildContentItemsFieldSet-ChildContentItemsFieldSetFeed
                alternates.Add($"{key.ContentType}{displayTypeDisplayToken}__{key.FieldSetType}__{shapeTypeSuffix}");

                if (hasStereotype)
                {
                    // [Stereotype]_[DisplayType]__[FieldSetType]__[ShapeType], e.g. Blog-ChildContentItemsFieldSet-ChildContentItemsFieldSetFeed
                    alternates.Add($"{key.Stereotype}{displayTypeDisplayToken}__{key.FieldSetType}__{shapeTypeSuffix}");
                }

                if (!fieldSetTypeEqualsFieldSetName)
                {
                    // [ContentType]_[DisplayType]__[FieldSetName]__[ShapeType], e.g. LandingPage-Categories-TaxonomySummary
                    lastAlternatesOfNamedFieldSet.Add($"{key.ContentType}{displayTypeDisplayToken}__{key.FieldSetName}__{shapeTypeSuffix}");

                    if (hasStereotype)
                    {
                        // [Stereotype]_[DisplayType]__[FieldSetType]__[FieldSetName]__[ShapeType], e.g. Widget-ServicePart-Categories-TaxonomySummary
                        lastAlternatesOfNamedFieldSet.Add($"{key.Stereotype}{displayTypeDisplayToken}__{key.FieldSetType}__{key.FieldSetName}__{shapeTypeSuffix}");
                    }
                }
            }

            alternates.AddRange(lastAlternatesOfNamedFieldSet);
        }

        return alternates.ToArray();
    }
}
