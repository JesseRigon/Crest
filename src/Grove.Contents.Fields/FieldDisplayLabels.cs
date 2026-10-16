using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Display.Management.Descriptors;
using Grove.Data.Localization.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Contents.Fields;

/// <summary>
/// The field displays' labels, data-localized as the Razor views localized them (the field's
/// display name under <see cref="DataLocalizationContext.ContentField"/>): set on each field
/// shape while it is displayed, ahead of the view model's plain display name, so the display
/// components read the localized label (docs/blazor-display.md rework step 7). A field the
/// attachment marks as not affected by culture (<see cref="Grove.Contents.Management.Metadata.Settings.FieldCultureSettings"/>)
/// is displayed culture-invariant.
/// </summary>
public sealed class FieldDisplayLabels : ShapeTableProvider
{
    /// <summary>The field shapes that carry a label, and their display-mode variants.</summary>
    public static readonly string[] FieldShapes =
    [
        "TextField", "HtmlField", "MarkdownField", "NumericField", "BooleanField", "DateField", "DateTimeField",
        "TimeField", "LinkField",
        "MultiTextField", "YoutubeField", "MediaField", "GeoPointField",
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

                    // A field culture does not affect (its attachment says so) formats the same in every culture.
                    if (!definition.IsCultureAffected())
                    {
                        context.Shape.Properties["CultureInvariant"] = true;
                    }
                });
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>The label a field display shows: its display name, data-localized.</summary>
    public static string Localize(IDataLocalizer localizer, ContentFieldDefinition definition)
    {
        var name = definition.DisplayName();
        var attachment = definition.ContentTypeFieldSetDefinition?.Name ?? definition.FieldSetDefinition.Name;
        var localized = localizer[name, DataLocalizationContext.ContentField(attachment)];
        return string.IsNullOrEmpty(localized?.Value) ? name : localized.Value;
    }

    // The view models expose the field's definition as FieldDefinition.
    private static ContentFieldDefinition? Definition(object shape) =>
        shape.GetType().GetProperty("FieldDefinition")?.GetValue(shape) as ContentFieldDefinition;
}
