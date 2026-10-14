using System.Globalization;
using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Crest.Components.Content;

/// <summary>A resolved reference a display shows: its text and its link (relative to the shell's base).</summary>
public sealed record DisplayLinkItem(string? Text, string? Url);

/// <summary>A media field's file as its driver resolved it for the viewer (its <c>/files</c> URL).</summary>
public sealed record MediaDisplayItem(string? Url, string? Name, bool IsImage, string? MediaText);

/// <summary>
/// The display of a content field (docs/blazor-display.md rework step 7): the component its
/// driver's shape binds to by name, reading the values the driver's view model exposes, so the
/// field no longer falls back to its Razor view. Every field renders as
/// <c>div.field.field-type-{type}.field-name-{part}-{field}</c>, labelled where the Razor view
/// labelled it.
/// </summary>
public abstract class FieldDisplayBase : ComponentBase
{
    /// <summary>The field's label (the field definition's display name).</summary>
    [Parameter]
    [ComponentParameter(Hidden = true)]
    public string? Label { get; set; }

    /// <summary>The field's CSS name, <c>{part}-{field}</c>.</summary>
    [Parameter]
    [ComponentParameter(Hidden = true)]
    public string? FieldClass { get; set; }

    protected abstract string FieldType { get; }

    /// <summary>Whether the label is shown (the Razor views of the reference fields show it).</summary>
    protected virtual bool ShowLabel => false;

    /// <summary>Whether there is anything to show; an empty field renders nothing.</summary>
    protected virtual bool HasValue => true;

    protected abstract void RenderValue(RenderTreeBuilder builder);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (!HasValue)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", $"field field-type-{FieldType}" + (string.IsNullOrEmpty(FieldClass) ? string.Empty : " field-name-" + FieldClass));
        if (ShowLabel && !string.IsNullOrEmpty(Label))
        {
            builder.OpenElement(2, "span");
            builder.AddAttribute(3, "class", "name");
            builder.AddContent(4, Label);
            builder.CloseElement();
        }

        builder.OpenRegion(5);
        RenderValue(builder);
        builder.CloseRegion();
        builder.CloseElement();
    }

    protected static void Values(RenderTreeBuilder builder, IReadOnlyList<string?> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "value");
            builder.AddContent(2, values[index]);
            builder.CloseElement();
            if (index < values.Count - 1)
            {
                builder.AddContent(3, ", ");
            }
        }
    }

    protected static void Links(RenderTreeBuilder builder, IReadOnlyList<DisplayLinkItem> items, string empty)
    {
        if (items.Count == 0)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "value");
            builder.AddContent(2, empty);
            builder.CloseElement();
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            builder.OpenElement(3, "span");
            builder.AddAttribute(4, "class", "value");
            builder.OpenElement(5, "a");
            builder.AddAttribute(6, "href", SafeUrl.IsSafe(items[index].Url) ? items[index].Url : "#");
            builder.AddContent(7, items[index].Text);
            builder.CloseElement();
            builder.CloseElement();
            if (index < items.Count - 1)
            {
                builder.AddContent(8, ", ");
            }
        }
    }
}

[BlazorComponent("TextField", DisplayName = "Text field", Category = "Fields", Icon = "short_text")]
public sealed class TextFieldDisplay : FieldDisplayBase
{
    [Parameter]
    public string? Text { get; set; }

    protected override string FieldType => "textfield";

    protected override void RenderValue(RenderTreeBuilder builder) => builder.AddContent(0, Text);
}

/// <summary>The text field's <c>Header</c> display mode: the text as a heading.</summary>
[BlazorComponent("TextField_Display__Header", DisplayName = "Text field as a heading", Category = "Fields", Icon = "title")]
public sealed class TextFieldHeaderDisplay : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    [ComponentParameter(Hidden = true)]
    public string? FieldClass { get; set; }

    [Parameter]
    [ComponentParameter(Choices = ["h1", "h2", "h3", "h4", "h5", "h6"])]
    public string Level { get; set; } = "h2";

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, Level is "h1" or "h2" or "h3" or "h4" or "h5" or "h6" ? Level : "h2");
        builder.AddAttribute(1, "class", "field field-type-textfield" + (string.IsNullOrEmpty(FieldClass) ? string.Empty : " field-name-" + FieldClass));
        builder.AddContent(2, Text);
        builder.CloseElement();
    }
}

[BlazorComponent("HtmlField", DisplayName = "HTML field", Category = "Fields", Icon = "code")]
public sealed class HtmlFieldDisplay : FieldDisplayBase
{
    /// <summary>The field's HTML as its driver rendered it; sanitized on the server (a markup parameter).</summary>
    [Parameter]
    [ComponentParameter(Editor = ComponentSchema.Editors.Markup, Hidden = true)]
    public string? Html { get; set; }

    protected override string FieldType => "htmlfield";

    protected override void RenderValue(RenderTreeBuilder builder) => builder.AddMarkupContent(0, Html ?? string.Empty);
}

[BlazorComponent("NumericField", DisplayName = "Number field", Category = "Fields", Icon = "pin")]
public sealed class NumericFieldDisplay : FieldDisplayBase
{
    [Parameter]
    public decimal? Value { get; set; }

    protected override string FieldType => "numericfield";

    protected override void RenderValue(RenderTreeBuilder builder) => builder.AddContent(0, Value?.ToString(CultureInfo.CurrentCulture));
}

[BlazorComponent("BooleanField", DisplayName = "Yes/no field", Category = "Fields", Icon = "check_box")]
public sealed class BooleanFieldDisplay : FieldDisplayBase
{
    [Parameter]
    public bool Value { get; set; }

    protected override string FieldType => "booleanfield";

    protected override bool ShowLabel => true;

    protected override void RenderValue(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "value");
        builder.AddContent(2, Value ? "Yes" : "No");
        builder.CloseElement();
    }
}

[BlazorComponent("DateField", DisplayName = "Date field", Category = "Fields", Icon = "event")]
public sealed class DateFieldDisplay : FieldDisplayBase
{
    [Parameter]
    public DateTime? Value { get; set; }

    protected override string FieldType => "datefield";

    protected override bool HasValue => Value.HasValue;

    protected override void RenderValue(RenderTreeBuilder builder) => builder.AddContent(0, Value?.ToString("d", CultureInfo.CurrentCulture));
}

[BlazorComponent("DateTimeField", DisplayName = "Date and time field", Category = "Fields", Icon = "schedule")]
public sealed class DateTimeFieldDisplay : FieldDisplayBase
{
    /// <summary>The value in the site's time zone, as the driver converted it.</summary>
    [Parameter]
    public DateTime? LocalDateTime { get; set; }

    protected override string FieldType => "datetimefield";

    protected override bool HasValue => LocalDateTime.HasValue;

    protected override void RenderValue(RenderTreeBuilder builder) => builder.AddContent(0, LocalDateTime?.ToString("g", CultureInfo.CurrentCulture));
}

[BlazorComponent("TimeField", DisplayName = "Time field", Category = "Fields", Icon = "schedule")]
public sealed class TimeFieldDisplay : FieldDisplayBase
{
    [Parameter]
    public TimeSpan? Value { get; set; }

    protected override string FieldType => "timefield";

    protected override bool HasValue => Value.HasValue;

    protected override void RenderValue(RenderTreeBuilder builder) => builder.AddContent(0, Value?.ToString("c", CultureInfo.InvariantCulture));
}

[BlazorComponent("LinkField", DisplayName = "Link field", Category = "Fields", Icon = "link")]
public sealed class LinkFieldDisplay : FieldDisplayBase
{
    [Parameter]
    public string? Url { get; set; }

    /// <summary>The text the field's settings choose (static, the URL, or the stored text).</summary>
    [Parameter]
    public string? LinkText { get; set; }

    [Parameter]
    public string? Target { get; set; }

    protected override string FieldType => "linkfield";

    protected override bool HasValue => !string.IsNullOrEmpty(Url);

    protected override void RenderValue(RenderTreeBuilder builder)
    {
        var href = Url!.StartsWith("~/", StringComparison.Ordinal) ? Url[2..] : Url;
        builder.OpenElement(0, "a");
        builder.AddAttribute(1, "href", SafeUrl.IsSafe(href) ? href : "#");
        builder.AddAttribute(2, "target", string.IsNullOrWhiteSpace(Target) ? "_self" : Target);
        if (Target == "_blank")
        {
            builder.AddAttribute(3, "rel", "noopener noreferrer");
        }

        builder.AddContent(4, string.IsNullOrEmpty(LinkText) ? Url : LinkText);
        builder.CloseElement();
    }
}

[BlazorComponent("ContentPickerField", DisplayName = "Content items field", Category = "Fields", Icon = "link")]
public sealed class ContentPickerFieldDisplay : FieldDisplayBase
{
    /// <summary>The picked items, resolved by the driver for the viewer.</summary>
    [Parameter]
    public IReadOnlyList<DisplayLinkItem>? Items { get; set; }

    protected override string FieldType => "contentpickerfield";

    protected override bool ShowLabel => true;

    protected override void RenderValue(RenderTreeBuilder builder) => Links(builder, Items ?? [], "No content items.");
}

[BlazorComponent("LocalizationSetContentPickerField", DisplayName = "Localized content items field", Category = "Fields", Icon = "translate")]
public sealed class LocalizationSetContentPickerFieldDisplay : FieldDisplayBase
{
    [Parameter]
    public IReadOnlyList<DisplayLinkItem>? Items { get; set; }

    protected override string FieldType => "localizationsetpickerfield";

    protected override bool ShowLabel => true;

    protected override void RenderValue(RenderTreeBuilder builder) => Links(builder, Items ?? [], "No content items.");
}

/// <summary>A user picker field, by the user names stored with it (both its display modes).</summary>
[BlazorComponent("UserPickerField", DisplayName = "Users field", Category = "Fields", Icon = "group")]
public sealed class UserPickerFieldDisplay : FieldDisplayBase
{
    [Parameter]
    public string[]? UserNames { get; set; }

    protected override string FieldType => "userpickerfield";

    protected override bool ShowLabel => true;

    protected override void RenderValue(RenderTreeBuilder builder)
    {
        if (UserNames is not { Length: > 0 } names)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "value");
            builder.AddContent(2, "No users.");
            builder.CloseElement();
            return;
        }

        Values(builder, names);
    }
}

[BlazorComponent("MultiTextField", DisplayName = "Multiple choice field", Category = "Fields", Icon = "checklist")]
public sealed class MultiTextFieldDisplay : FieldDisplayBase
{
    [Parameter]
    public string[]? Values { get; set; }

    protected override string FieldType => "multitextfield";

    protected override bool ShowLabel => true;

    protected override bool HasValue => Values is { Length: > 0 };

    protected override void RenderValue(RenderTreeBuilder builder) => FieldDisplayBase.Values(builder, Values!);
}

[BlazorComponent("YoutubeField", DisplayName = "YouTube field", Category = "Fields", Icon = "smart_display")]
public sealed class YoutubeFieldDisplay : FieldDisplayBase
{
    [Parameter]
    public string? EmbeddedAddress { get; set; }

    [Parameter]
    public int Width { get; set; } = 640;

    [Parameter]
    public int Height { get; set; } = 360;

    protected override string FieldType => "youtubefield";

    // Only YouTube's own embed addresses: the field stores what an editor pasted.
    protected override bool HasValue =>
        Uri.TryCreate(EmbeddedAddress, UriKind.Absolute, out var address)
        && address.Scheme == Uri.UriSchemeHttps
        && address.Host is "www.youtube.com" or "youtube.com" or "www.youtube-nocookie.com";

    protected override void RenderValue(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "iframe");
        builder.AddAttribute(1, "width", Math.Clamp(Width, 1, 4096));
        builder.AddAttribute(2, "height", Math.Clamp(Height, 1, 4096));
        builder.AddAttribute(3, "src", EmbeddedAddress);
        builder.AddAttribute(4, "frameborder", "0");
        builder.AddAttribute(5, "allow", "autoplay; encrypted-media");
        builder.AddAttribute(6, "allowfullscreen", true);
        builder.CloseElement();
    }
}

[BlazorComponent("MediaField", DisplayName = "Media field", Category = "Fields", Icon = "perm_media")]
public sealed class MediaFieldDisplay : FieldDisplayBase
{
    /// <summary>The files, resolved by the driver through the access check, each with its <c>/files</c> URL.</summary>
    [Parameter]
    public IReadOnlyList<MediaDisplayItem>? Items { get; set; }

    protected override string FieldType => "mediafield";

    protected override bool ShowLabel => true;

    protected override bool HasValue => Items is { Count: > 0 };

    protected override void RenderValue(RenderTreeBuilder builder)
    {
        foreach (var item in Items!)
        {
            if (!SafeUrl.IsSafe(item.Url))
            {
                continue;
            }

            if (item.IsImage)
            {
                builder.OpenElement(0, "img");
                builder.AddAttribute(1, "src", item.Url);
                builder.AddAttribute(2, "alt", item.MediaText ?? string.Empty);
                builder.CloseElement();
            }
            else
            {
                builder.OpenElement(3, "a");
                builder.AddAttribute(4, "href", item.Url);
                builder.AddContent(5, string.IsNullOrEmpty(item.MediaText) ? item.Name : item.MediaText);
                builder.CloseElement();
            }
        }
    }
}

/// <summary>An item's title (the <c>TitlePart</c> shape): a heading, linked to the item in a summary.</summary>
[BlazorComponent("TitlePart", DisplayName = "Title", Category = "Content", Icon = "title")]
public sealed class TitlePartDisplay : ComponentBase
{
    [Parameter]
    public string? Title { get; set; }

    /// <summary>The item's link; the summary display links the title to it.</summary>
    [Parameter]
    [ComponentParameter(Hidden = true)]
    public string? Url { get; set; }

    [Parameter]
    [ComponentParameter(Choices = ["h1", "h2"])]
    public string Level { get; set; } = "h1";

    [Parameter]
    public bool Linked { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, Level == "h2" ? "h2" : "h1");
        if (Linked && SafeUrl.IsSafe(Url))
        {
            builder.OpenElement(1, "a");
            builder.AddAttribute(2, "href", Url);
            builder.AddContent(3, Title);
            builder.CloseElement();
        }
        else
        {
            builder.AddContent(4, Title);
        }

        builder.CloseElement();
    }
}

/// <summary>The summary display of a title: an <c>h2</c> linked to the item.</summary>
[BlazorComponent("TitlePart_Summary", DisplayName = "Title (summary)", Category = "Content", Icon = "title")]
public sealed class TitlePartSummaryDisplay : ComponentBase
{
    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    [ComponentParameter(Hidden = true)]
    public string? Url { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "h2");
        if (SafeUrl.IsSafe(Url))
        {
            builder.OpenElement(1, "a");
            builder.AddAttribute(2, "href", Url);
            builder.AddContent(3, Title);
            builder.CloseElement();
        }
        else
        {
            builder.AddContent(4, Title);
        }

        builder.CloseElement();
    }
}

/// <summary>A bag of contained items (the <c>BagPart</c> shape): each item's display, built by its drivers, in order.</summary>
[BlazorComponent("BagPart", DisplayName = "Contained items", Category = "Content", Icon = "inventory_2")]
public sealed class BagPartDisplay : ComponentBase
{
    [Parameter]
    [ComponentZone]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "section");
        builder.AddAttribute(1, "class", "flow");
        builder.AddContent(2, ChildContent);
        builder.CloseElement();
    }
}

/// <summary>An empty bag renders nothing.</summary>
[BlazorComponent("BagPart_Empty", DisplayName = "No contained items", Category = "Content", Icon = "inventory_2")]
public sealed class BagPartEmptyDisplay : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }
}

[BlazorComponent("MarkdownField", DisplayName = "Markdown field", Category = "Fields", Icon = "notes")]
public sealed class MarkdownFieldDisplay : FieldDisplayBase
{
    /// <summary>The Markdown as its driver rendered it to HTML; sanitized on the server (a markup parameter).</summary>
    [Parameter]
    [ComponentParameter(Editor = ComponentSchema.Editors.Markup, Hidden = true)]
    public string? Html { get; set; }

    protected override string FieldType => "markdownfield";

    protected override void RenderValue(RenderTreeBuilder builder) => builder.AddMarkupContent(0, Html ?? string.Empty);
}

/// <summary>A taxonomy field: its terms, linked (the blog's tags and categories).</summary>
[BlazorComponent("TaxonomyField", DisplayName = "Taxonomy field", Category = "Fields", Icon = "sell")]
public sealed class TaxonomyFieldDisplay : FieldDisplayBase
{
    [Parameter]
    public IReadOnlyList<DisplayLinkItem>? Items { get; set; }

    protected override string FieldType => "taxonomyfield";

    protected override bool ShowLabel => true;

    protected override bool HasValue => Items is { Count: > 0 };

    protected override void RenderValue(RenderTreeBuilder builder) => Links(builder, Items!, string.Empty);
}

/// <summary>A list's contained items (the <c>ListPart</c> shape): their summaries, as their drivers built them.</summary>
[BlazorComponent("ListPart", DisplayName = "List items", Category = "Content", Icon = "list")]
public sealed class ListPartDisplay : ComponentBase
{
    [Parameter]
    [ComponentZone]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "crest-list");
        builder.AddContent(2, ChildContent);
        builder.CloseElement();
    }
}

/// <summary>A geographic point field: its coordinates, linked to a map.</summary>
[BlazorComponent("GeoPointField", DisplayName = "Location field", Category = "Fields", Icon = "location_on")]
public sealed class GeoPointFieldDisplay : FieldDisplayBase
{
    [Parameter]
    public decimal? Latitude { get; set; }

    [Parameter]
    public decimal? Longitude { get; set; }

    protected override string FieldType => "geopointfield";

    protected override bool HasValue => Latitude.HasValue && Longitude.HasValue;

    protected override void RenderValue(RenderTreeBuilder builder)
    {
        var latitude = Latitude!.Value.ToString("N6", CultureInfo.InvariantCulture);
        var longitude = Longitude!.Value.ToString("N6", CultureInfo.InvariantCulture);
        builder.OpenElement(0, "a");
        builder.AddAttribute(1, "href", $"https://www.openstreetmap.org/?mlat={latitude}&mlon={longitude}#map=15/{latitude}/{longitude}");
        builder.AddAttribute(2, "target", "_blank");
        builder.AddAttribute(3, "rel", "noopener noreferrer");
        builder.AddContent(4, Latitude.Value.ToString("N6", CultureInfo.CurrentCulture) + ", " + Longitude.Value.ToString("N6", CultureInfo.CurrentCulture));
        builder.CloseElement();
    }
}

/// <summary>A term in a taxonomy's tree: text, link, child terms.</summary>
public sealed record TaxonomyTermNode(string? Text, string? Url, IReadOnlyList<TaxonomyTermNode>? Children);

/// <summary>A taxonomy's own page (the <c>TaxonomyPart</c> shape): its term tree, each term linked.</summary>
[BlazorComponent("TaxonomyPart", DisplayName = "Taxonomy terms", Category = "Content", Icon = "account_tree")]
public sealed class TaxonomyPartDisplay : ComponentBase
{
    [Parameter]
    public IReadOnlyList<TaxonomyTermNode>? Terms { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "section");
        builder.AddAttribute(1, "class", "taxonomy");
        Tree(builder, Terms ?? []);
        builder.CloseElement();
    }

    private static void Tree(RenderTreeBuilder builder, IReadOnlyList<TaxonomyTermNode> terms)
    {
        if (terms.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "ul");
        foreach (var term in terms)
        {
            builder.OpenElement(1, "li");
            builder.OpenElement(2, "a");
            builder.AddAttribute(3, "href", SafeUrl.IsSafe(term.Url) ? term.Url : "#");
            builder.AddContent(4, term.Text);
            builder.CloseElement();
            builder.OpenRegion(5);
            Tree(builder, term.Children ?? []);
            builder.CloseRegion();
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}

/// <summary>A taxonomy without terms renders nothing.</summary>
[BlazorComponent("TaxonomyPart_Empty", DisplayName = "No terms", Category = "Content", Icon = "account_tree")]
public sealed class TaxonomyPartEmptyDisplay : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }
}
