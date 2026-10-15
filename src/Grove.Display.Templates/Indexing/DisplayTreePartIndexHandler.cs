using Grove.Data.Indexing;
using Grove.Display.Templates.Models;
using Grove.Display.Templates.Services;

namespace Grove.Display.Templates.Indexing;

/// <summary>
/// An item's page body in the search indexes: its text (<see cref="DisplayTreeText"/>, a migrated
/// Liquid body's composite source included), sanitized as markup.
/// </summary>
public sealed class DisplayTreePartIndexHandler(ITemplateCatalog templates) : ContentPartIndexHandler<DisplayTreePart>
{
    public override async Task BuildIndexAsync(DisplayTreePart part, BuildPartIndexContext context)
    {
        if (part.Tree is null)
        {
            return;
        }

        var composites = (await templates.ListPublishedAsync())
            .Where(template => template.IsComposite)
            .GroupBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Source, StringComparer.OrdinalIgnoreCase);
        var text = DisplayTreeText.Extract(part.Tree, composites);
        var options = context.Settings.ToOptions() | DocumentIndexOptions.Sanitize;
        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, text, options);
        }
    }
}
