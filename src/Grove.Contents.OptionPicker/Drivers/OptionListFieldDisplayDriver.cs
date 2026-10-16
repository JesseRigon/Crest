using System.Text.Encodings.Web;
using Fluid;
using Fluid.Values;
using Grove.Contents.Fields.ViewModels;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;
using Grove.Fields;
using Grove.Display.Liquid;
using Grove.Models;
using Grove.Services;
using Grove.Infrastructure.Settings;

namespace Grove.Contents.OptionPicker.Drivers;

/// <summary>
/// The display of an option picker field: its selection as the <c>OptionListField</c> shape, which
/// the field display component of that name renders (label and items). Content items are the
/// published ones, labelled by the attachment's title pattern and linked to their page; other
/// sources' rows are labelled by the attachment's first visible column, else the source's default,
/// and linked to their page where the source gives one (taxonomy terms). The <c>Tags</c> display
/// mode renders the same items as badges.
/// </summary>
public sealed class OptionListFieldDisplayDriver(
    IContentManager contentManager,
    IEnumerable<IOptionSourceProvider> providers,
    ILiquidTemplateManager liquid) : ContentFieldDisplayDriver<OptionListField>
{
    public override IDisplayResult Display(OptionListField field, BuildFieldDisplayContext context)
    {
        return Initialize<OptionListFieldViewModel>(GetDisplayShapeType(context), async model =>
        {
            var settings = context.FieldDefinition.GetSettings<OptionListFieldSettings>();
            model.Field = field;
            model.FieldSet = context.ContentFieldSet;
            model.FieldDefinition = context.FieldDefinition;
            model.Items = await ItemsAsync(field, settings);

            // "Items" is also a shape's own member (its children): the display component reads
            // the selection from the shape's properties.
            if (model is Grove.Display.Management.IShape shape)
            {
                shape.Properties[nameof(OptionListFieldViewModel.Items)] = model.Items;
            }
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    private async Task<IReadOnlyList<DisplayLinkItem>> ItemsAsync(OptionListField field, OptionListFieldSettings settings)
    {
        if (field.SelectedIds is not { Length: > 0 } ids)
        {
            return [];
        }

        var sourceKey = string.IsNullOrEmpty(field.SourceKey) ? settings.SourceKey : field.SourceKey;
        var (providerKey, qualifier) = OptionSourceKeys.Split(sourceKey);
        if (providerKey == OptionSourceKeys.ContentItemProvider)
        {
            var items = (await contentManager.GetAsync(ids)).ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);
            var links = new List<DisplayLinkItem>();
            foreach (var id in ids)
            {
                if (items.TryGetValue(id, out var item))
                {
                    links.Add(new DisplayLinkItem(await TitleAsync(item, settings.TitlePattern), ContentItemLinks.SitePath(item)));
                }
            }

            return links;
        }

        var provider = providers.FirstOrDefault(candidate => string.Equals(candidate.Key, providerKey, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            return [];
        }

        var column = settings.Columns.FirstOrDefault(candidate => candidate.Visible)?.Path
            ?? (await provider.DescribeColumnsAsync(qualifier)).FirstOrDefault(candidate => candidate.IsDefaultDisplay)?.Path
            ?? "DisplayText";
        var rows = await provider.GetByIdsAsync(qualifier, ids, [column]);
        // In the field's order; a source whose records have pages (taxonomy terms) links them.
        var byId = rows.ToDictionary(row => row.Id, StringComparer.Ordinal);
        return [.. ids.Where(byId.ContainsKey).Select(id => new DisplayLinkItem(byId[id].Values.GetValueOrDefault(column) ?? id, byId[id].Url!))];
    }

    private async Task<string> TitleAsync(ContentItem item, string? pattern) =>
        string.IsNullOrWhiteSpace(pattern)
            ? item.DisplayText ?? item.ContentItemId
            : (await liquid.RenderStringAsync(pattern, NullEncoder.Default, item,
                new Dictionary<string, FluidValue> { [nameof(ContentItem)] = new ObjectValue(item) })).Trim();
}

/// <summary>What the <c>OptionListField</c> display shape carries.</summary>
public class OptionListFieldViewModel
{
    public OptionListField Field { get; set; } = default!;

    public ContentFieldSet FieldSet { get; set; } = default!;

    public ContentFieldDefinition FieldDefinition { get; set; } = default!;

    /// <summary>The selection resolved for the viewer (text, and the link where there is one).</summary>
    public IReadOnlyList<DisplayLinkItem> Items { get; set; } = [];

    /// <summary>The selected ids, as Liquid reads them (<c>Model.SelectedIds</c>).</summary>
    public string[] SelectedIds => Field?.SelectedIds ?? [];

    public string? Label => FieldDefinition?.DisplayName();

    public string? FieldClass => FieldDefinition is null ? null : Grove.Utilities.StringExtensions.HtmlClassify(FieldDefinition.FieldSetDefinition.Name + "-" + FieldDefinition.Name);
}
