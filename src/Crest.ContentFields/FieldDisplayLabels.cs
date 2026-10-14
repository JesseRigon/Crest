using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Descriptors;
using Crest.Localization.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.ContentFields;

/// <summary>
/// The field displays' labels, data-localized as the Razor views localized them (the field's
/// display name under <see cref="DataLocalizationContext.ContentField"/>): set on each field
/// shape while it is displayed, ahead of the view model's plain display name, so the display
/// components read the localized label (docs/blazor-display.md rework step 7).
/// </summary>
public sealed class FieldDisplayLabels : ShapeTableProvider
{
    /// <summary>The field shapes that carry a label, and their display-mode variants.</summary>
    public static readonly string[] FieldShapes =
    [
        "TextField", "HtmlField", "MarkdownField", "NumericField", "BooleanField", "DateField", "DateTimeField",
        "TimeField", "LinkField", "ContentPickerField", "LocalizationSetContentPickerField", "UserPickerField",
        "MultiTextField", "YoutubeField", "MediaField", "TaxonomyField", "GeoPointField",
    ];

    public override ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        foreach (var shape in FieldShapes)
        {
            foreach (var type in new[] { shape, shape + "_Display" })
            {
                builder.Describe(type).OnDisplaying(context =>
                {
                    if (Definition(context.Shape) is not { } definition || context.ServiceProvider?.GetService<IDataLocalizer>() is not { } localizer)
                    {
                        return;
                    }

                    context.Shape.Properties["Label"] = Localize(localizer, definition);
                });
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>The label a field display shows: its display name, data-localized.</summary>
    public static string Localize(IDataLocalizer localizer, ContentPartFieldDefinition definition)
    {
        var name = definition.DisplayName();
        var localized = localizer[name, DataLocalizationContext.ContentField(definition.PartDefinition.Name)];
        return string.IsNullOrEmpty(localized?.Value) ? name : localized.Value;
    }

    // The view models expose the field's definition as PartFieldDefinition.
    private static ContentPartFieldDefinition? Definition(object shape) =>
        shape.GetType().GetProperty("PartFieldDefinition")?.GetValue(shape) as ContentPartFieldDefinition;
}
