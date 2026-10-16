using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Display.Components.Primitives;
using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Grove.Display.Components.Editors;

/// <summary>
/// A list's contained items in its editor (docs/MVC/parity.md › New components): a page of the
/// children (searched, filtered by status), each opening its own editor; moved up and down when
/// the list is ordered; a child created as one of the list's contained types on the native create
/// page with the list as its container. Below them, the list's feed values (its RSS link, a proxy
/// address, the number of feed items). The children come from Grove.Contents.Hierarchy'
/// <c>api/grove/lists/{id}/items</c>; an unsaved list has none yet.
/// </summary>
[BlazorComponent("ChildContentItemsFieldSetEditor", DisplayName = "List items", Category = "Editors", Icon = "list", Edits = ["ChildContentItems"])]
public sealed class ChildContentItemsFieldSetEditor : FieldEditorBase
{
    private static readonly JsonSerializerOptions s_web = new(JsonSerializerDefaults.Web);

    private ListPageModel? _page;
    private string? _search;
    private string? _status;
    private int _pageNumber = 1;
    private bool _loaded;
    private string? _error;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    protected override async Task OnParametersSetAsync()
    {
        if (_loaded || Context?.ContentItemId is null)
        {
            return;
        }

        _loaded = true;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (Context?.Http is not { } http || Context.ContentItemId is not { } id)
        {
            return;
        }

        var query = $"page={_pageNumber}"
            + (string.IsNullOrWhiteSpace(_search) ? string.Empty : "&search=" + Uri.EscapeDataString(_search))
            + (string.IsNullOrWhiteSpace(_status) ? string.Empty : "&status=" + Uri.EscapeDataString(_status));
        try
        {
            using var response = await http.GetAsync($"api/grove/lists/{Uri.EscapeDataString(id)}/items?{query}");
            _page = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ListPageModel>(s_web) : null;
            _error = _page is null ? "The list's items could not be loaded." : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _error = "The list's items could not be loaded.";
        }
    }

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-list-editor");
        if (Context?.ContentItemId is null)
        {
            builder.OpenElement(2, "p");
            builder.AddContent(3, "Save the list to add items to it.");
            builder.CloseElement();
        }
        else
        {
            RenderItems(builder);
        }

        RenderFeed(builder);
        builder.CloseElement();
    }

    private void RenderItems(RenderTreeBuilder builder)
    {
        builder.OpenElement(10, "div");
        builder.AddAttribute(11, "class", "grove-editor__pair");
        builder.OpenComponent<GroveTextBox>(12);
        builder.AddComponentParameter(13, nameof(GroveTextBox.Value), _search);
        builder.AddComponentParameter(14, nameof(GroveTextBox.Placeholder), "Search");
        builder.AddComponentParameter(15, nameof(GroveTextBox.Change), EventCallback.Factory.Create<string>(this, async value => { _search = value; _pageNumber = 1; await LoadAsync(); }));
        builder.CloseComponent();
        builder.OpenComponent<GroveDropDown<string>>(16);
        builder.AddComponentParameter(17, "Data", new[] { "latest", "published", "draft" });
        builder.AddComponentParameter(18, "Value", _status ?? "latest");
        builder.AddComponentParameter(19, "ValueChanged", EventCallback.Factory.Create<string>(this, async value => { _status = value; _pageNumber = 1; await LoadAsync(); }));
        builder.CloseComponent();
        foreach (var type in _page?.ContainedTypes ?? [])
        {
            builder.OpenComponent<GroveButton>(20);
            builder.SetKey(type.Name);
            builder.AddComponentParameter(21, nameof(GroveButton.Text), "New " + type.DisplayName);
            builder.AddComponentParameter(22, nameof(GroveButton.Icon), "add");
            builder.AddComponentParameter(23, nameof(GroveButton.Size), ButtonSize.Small);
            builder.AddComponentParameter(24, nameof(GroveButton.ButtonStyle), ButtonStyle.Light);
            builder.AddComponentParameter(25, nameof(GroveButton.Disabled), Disabled);
            builder.AddComponentParameter(26, nameof(GroveButton.Click), EventCallback.Factory.Create<MouseEventArgs>(this, () =>
                Navigation.NavigateTo($"Contents/ContentTypes/{Uri.EscapeDataString(type.Name)}/Create?containerId={Uri.EscapeDataString(Context!.ContentItemId!)}")));
            builder.CloseComponent();
        }

        builder.CloseElement();

        if (_error is not null)
        {
            builder.OpenElement(30, "div");
            builder.AddAttribute(31, "class", "grove-editor__error rz-color-danger");
            builder.AddContent(32, _error);
            builder.CloseElement();
        }

        if (_page is null)
        {
            return;
        }

        builder.OpenElement(40, "table");
        builder.AddAttribute(41, "class", "grove-list-editor__items");
        for (var index = 0; index < _page.Items.Count; index++)
        {
            var position = index;
            var item = _page.Items[index];
            builder.OpenElement(42, "tr");
            builder.SetKey(item.ContentItemId);
            builder.OpenElement(43, "td");
            builder.OpenElement(44, "a");
            builder.AddAttribute(45, "href", $"Contents/ContentItems/{Uri.EscapeDataString(item.ContentItemId)}/Edit");
            builder.AddContent(46, string.IsNullOrEmpty(item.DisplayText) ? item.ContentType : item.DisplayText);
            builder.CloseElement();
            builder.CloseElement();
            builder.OpenElement(47, "td");
            builder.AddContent(48, item.ContentType);
            builder.CloseElement();
            builder.OpenElement(49, "td");
            builder.AddContent(50, item.Published ? "Published" : "Draft");
            builder.CloseElement();
            builder.OpenElement(51, "td");
            builder.AddContent(52, item.ModifiedUtc?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
            builder.CloseElement();
            if (_page.EnableOrdering)
            {
                builder.OpenElement(53, "td");
                Button(builder, 54, "arrow_upward", "Move up", Disabled || position == 0, () => MoveAsync(position, -1));
                Button(builder, 55, "arrow_downward", "Move down", Disabled || position == _page.Items.Count - 1, () => MoveAsync(position, 1));
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();

        var pages = Math.Max(1, (int)Math.Ceiling(_page.Total / (double)Math.Max(1, _page.PageSize)));
        if (pages > 1)
        {
            builder.OpenElement(60, "div");
            builder.AddAttribute(61, "class", "grove-list-editor__pager");
            Button(builder, 62, "chevron_left", "Previous page", _pageNumber <= 1, async () => { _pageNumber--; await LoadAsync(); });
            builder.AddContent(63, $" {_pageNumber} / {pages} ");
            Button(builder, 64, "chevron_right", "Next page", _pageNumber >= pages, async () => { _pageNumber++; await LoadAsync(); });
            builder.CloseElement();
        }
    }

    // The feed values are the part's own (the list's RSS link and its feed).
    private void RenderFeed(RenderTreeBuilder builder)
    {
        var part = Value;
        builder.OpenElement(70, "div");
        builder.AddAttribute(71, "class", "grove-list-editor__feed");
        builder.OpenElement(72, "label");
        builder.OpenElement(73, "input");
        builder.AddAttribute(74, "type", "checkbox");
        builder.AddAttribute(75, "checked", Read("DisableRssFeed.Value") is JsonValue disabled && disabled.TryGetValue<bool>(out var off) && off);
        builder.AddAttribute(76, "disabled", Disabled);
        builder.AddAttribute(77, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, change => Set("DisableRssFeed.Value", JsonValue.Create(change.Value is true))));
        builder.CloseElement();
        builder.AddContent(78, " Disable the RSS feed link");
        builder.CloseElement();
        builder.OpenElement(79, "div");
        builder.AddAttribute(80, "class", "grove-editor__pair");
        TextInput(builder, 81, "FeedProxyUrl.Text", "Feed proxy URL");
        builder.OpenComponent<GroveNumeric<int?>>(90);
        builder.AddComponentParameter(91, "Value", Read("FeedItemsCount.Value") is JsonValue count && count.TryGetValue<int>(out var items) ? items : (int?)null);
        builder.AddComponentParameter(92, "ValueChanged", EventCallback.Factory.Create<int?>(this, value => Set("FeedItemsCount.Value", value is null ? null : JsonValue.Create(value.Value))));
        builder.AddComponentParameter(93, "Placeholder", "Feed items (20)");
        builder.AddComponentParameter(94, "Min", 1m);
        builder.AddComponentParameter(95, "Disabled", Disabled);
        builder.CloseComponent();
        builder.CloseElement();
        builder.CloseElement();
    }

    private async Task MoveAsync(int index, int offset)
    {
        if (_page is null || Context?.Http is not { } http || Context.ContentItemId is not { } id)
        {
            return;
        }

        var ids = _page.Items.Select(item => item.ContentItemId).ToList();
        (ids[index], ids[index + offset]) = (ids[index + offset], ids[index]);
        try
        {
            using var response = await http.PostAsJsonAsync($"api/grove/lists/{Uri.EscapeDataString(id)}/order", new { ContentItemIds = ids });
            _error = response.IsSuccessStatusCode ? null : "The items could not be reordered.";
        }
        catch (HttpRequestException)
        {
            _error = "The items could not be reordered.";
        }

        await LoadAsync();
    }

    private void Button(RenderTreeBuilder builder, int sequence, string icon, string title, bool disabled, Func<Task> click)
    {
        builder.OpenComponent<GroveButton>(sequence);
        builder.AddComponentParameter(sequence + 100, nameof(GroveButton.Icon), icon);
        builder.AddComponentParameter(sequence + 101, nameof(GroveButton.Variant), Variant.Text);
        builder.AddComponentParameter(sequence + 102, nameof(GroveButton.Size), ButtonSize.ExtraSmall);
        builder.AddComponentParameter(sequence + 103, nameof(GroveButton.Disabled), disabled);
        builder.AddComponentParameter(sequence + 104, "title", title);
        builder.AddComponentParameter(sequence + 105, nameof(GroveButton.Click), EventCallback.Factory.Create<MouseEventArgs>(this, click));
        builder.CloseComponent();
    }

    /// <summary>A page of the list as the lists API serves it.</summary>
    public sealed record ListPageModel(IReadOnlyList<ListItemModel> Items, int Total, int Page, int PageSize, bool EnableOrdering, IReadOnlyList<ListTypeModel> ContainedTypes);

    public sealed record ListItemModel(string ContentItemId, string ContentType, string? DisplayText, bool Published, bool Latest, DateTime? ModifiedUtc, int Order);

    public sealed record ListTypeModel(string Name, string DisplayName);
}

/// <summary>
/// The SEO part (docs/MVC/parity.md › New components): the page title, whether the meta tags
/// render, the description, keywords, canonical address and robots, custom meta tags, the
/// default social image; the Open Graph, Twitter card and Google schema sections when the type's
/// part settings show them. The canonical address and the schema are checked as you type.
/// </summary>
[BlazorComponent("SeoMetaFieldSetEditor", DisplayName = "SEO", Category = "Editors", Icon = "travel_explore", Edits = ["SeoMeta"])]
public sealed class SeoMetaFieldSetEditor : FieldEditorBase
{
    private static readonly char[] s_invalidCanonical = "?#[]@!$&'()*+,;=<>\\|% ".ToCharArray();

    private bool Shows(string setting) =>
        Context?.FieldSet(FieldSetName)?.Settings?["SeoMetaFieldSetSettings"]?[setting] is JsonValue value && value.TryGetValue<bool>(out var shown) && shown;

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-seo-editor");

        Labelled(builder, 10, "Page title", child => TextInput(child, 0, "PageTitle.Text"));
        Labelled(builder, 20, "Render meta tags", child =>
        {
            child.OpenComponent<GroveSwitch>(0);
            child.AddComponentParameter(1, nameof(GroveSwitch.Value), !(Read("Render.Value") is JsonValue render && render.TryGetValue<bool>(out var on) && !on));
            child.AddComponentParameter(2, nameof(GroveSwitch.ValueChanged), EventCallback.Factory.Create<bool>(this, value => Set("Render.Value", JsonValue.Create(value))));
            child.AddComponentParameter(3, nameof(GroveSwitch.Disabled), Disabled);
            child.CloseComponent();
        });
        Labelled(builder, 30, "Meta description", child => TextAreaInput(child, 0, "MetaDescription.Text", 3));
        if (Shows("DisplayKeywords"))
        {
            Labelled(builder, 40, "Meta keywords", child => TextInput(child, 0, "MetaKeywords.Text"));
        }

        Labelled(builder, 50, "Canonical address", child => TextInput(child, 0, "Canonical.Text"));
        if (Text("Canonical.Text") is { Length: > 0 } canonical && canonical.IndexOfAny(s_invalidCanonical) > -1)
        {
            Error(builder, 55, "The canonical address contains characters it may not hold.");
        }

        Labelled(builder, 60, "Meta robots", child => TextInput(child, 0, "MetaRobots.Text", "index, follow"));
        if (Shows("DisplayCustomMetaTags"))
        {
            Labelled(builder, 70, "Custom meta tags", child =>
            {
                child.OpenComponent<CodeEditor>(0);
                child.AddComponentParameter(1, nameof(CodeEditor.Value), Value?["CustomMetaTags"]?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "[]");
                child.AddComponentParameter(2, nameof(CodeEditor.Changed), EventCallback.Factory.Create<string?>(this, CustomMetaTags));
                child.AddComponentParameter(3, nameof(CodeEditor.Language), "json");
                child.AddComponentParameter(4, nameof(CodeEditor.Disabled), Disabled);
                child.AddComponentParameter(5, nameof(CodeEditor.Height), "8rem");
                child.CloseComponent();
            });
        }

        Image(builder, 80, "DefaultSocialImage", "Default social image");

        if (Shows("DisplayOpenGraph"))
        {
            Section(builder, 100, "Open Graph", child =>
            {
                Labelled(child, 0, "Type", inner => TextInput(inner, 0, "OpenGraphType.Text", "website"));
                Labelled(child, 10, "Title", inner => TextInput(inner, 0, "OpenGraphTitle.Text"));
                Labelled(child, 20, "Description", inner => TextAreaInput(inner, 0, "OpenGraphDescription.Text", 2));
                Image(child, 30, "OpenGraphImage", "Image");
            });
        }

        if (Shows("DisplayTwitter"))
        {
            Section(builder, 120, "Twitter card", child =>
            {
                Labelled(child, 0, "Card", inner => TextInput(inner, 0, "TwitterCard.Text", "summary_large_image"));
                Labelled(child, 10, "Creator", inner => TextInput(inner, 0, "TwitterCreator.Text", "@"));
                Labelled(child, 20, "Site", inner => TextInput(inner, 0, "TwitterSite.Text", "@"));
                Labelled(child, 30, "Title", inner => TextInput(inner, 0, "TwitterTitle.Text"));
                Labelled(child, 40, "Description", inner => TextAreaInput(inner, 0, "TwitterDescription.Text", 2));
                Image(child, 50, "TwitterImage", "Image");
            });
        }

        if (Shows("DisplayGoogleSchema"))
        {
            Section(builder, 140, "Google schema", child =>
            {
                CodeInput(child, 0, "GoogleSchema.Text", "json");
                if (Text("GoogleSchema.Text") is { Length: > 0 } schema && !IsJson(schema))
                {
                    Error(child, 10, "The schema is not valid JSON.");
                }
            });
        }

        builder.CloseElement();
    }

    private void CustomMetaTags(string? text)
    {
        try
        {
            if (JsonNode.Parse(string.IsNullOrWhiteSpace(text) ? "[]" : text) is JsonArray tags)
            {
                Set("CustomMetaTags", tags);
            }
        }
        catch (JsonException)
        {
        }
    }

    private static bool IsJson(string text)
    {
        try
        {
            JsonNode.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // A social image is a media field inside the part: the media field editor edits it in place.
    private void Image(RenderTreeBuilder builder, int sequence, string property, string label)
    {
        builder.OpenComponent<Files.MediaFieldEditor>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(FieldSetName), FieldSetName);
        builder.AddComponentParameter(sequence + 2, nameof(FieldName), property);
        builder.AddComponentParameter(sequence + 3, nameof(Label), label);
        builder.CloseComponent();
    }

    private static void Labelled(RenderTreeBuilder builder, int sequence, string label, RenderFragment input)
    {
        builder.OpenComponent<GroveFormField>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(GroveFormField.Text), label);
        builder.AddComponentParameter(sequence + 2, nameof(GroveFormField.Style), "width: 100%;");
        builder.AddComponentParameter(sequence + 3, nameof(GroveFormField.ChildContent), input);
        builder.CloseComponent();
    }

    private static void Section(RenderTreeBuilder builder, int sequence, string title, RenderFragment content)
    {
        builder.OpenComponent<GroveFieldset>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(GroveFieldset.Text), title);
        builder.AddComponentParameter(sequence + 2, nameof(GroveFieldset.AllowCollapse), true);
        builder.AddComponentParameter(sequence + 3, nameof(GroveFieldset.ChildContent), content);
        builder.CloseComponent();
    }

    private static void Error(RenderTreeBuilder builder, int sequence, string message)
    {
        builder.OpenElement(sequence, "div");
        builder.AddAttribute(sequence + 1, "class", "grove-editor__error rz-color-danger");
        builder.AddContent(sequence + 2, message);
        builder.CloseElement();
    }
}

/// <summary>
/// A location (docs/MVC/parity.md › New components): latitude and longitude typed in, or, with
/// the field's map editor (<c>Leaflet</c>, <c>Map</c>), placed by clicking a map: OpenStreetMap
/// through Leaflet (<see cref="Content.LeafletMap"/>, no key), or Google Maps when the field's
/// settings name a key (<c>GeoPointFieldSettings.GoogleMapsApiKey</c>).
/// </summary>
[BlazorComponent("GeoPointFieldEditor", DisplayName = "Location", Category = "Editors", Icon = "location_on", Edits = ["GeoPointField"])]
public sealed class GeoPointFieldEditor : FieldEditorBase
{
    private decimal? Coordinate(string property) =>
        Value?[property] is JsonValue value && (value.TryGetValue<decimal>(out var number) || decimal.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)) ? number : null;

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-editor__pair");
        Number(builder, 10, "Latitude", -90, 90);
        Number(builder, 20, "Longitude", -180, 180);
        builder.CloseElement();

        if (!IsMode("Leaflet") && !IsMode("Map"))
        {
            return;
        }

        var latitude = Coordinate("Latitude");
        var longitude = Coordinate("Longitude");

        // OpenStreetMap through Leaflet by default; Google Maps when the field's settings name a key.
        if (Setting("GeoPointFieldSettings", "GoogleMapsApiKey")?.ToString() is not { Length: > 0 } apiKey)
        {
            builder.OpenComponent<Content.LeafletMap>(40);
            builder.AddComponentParameter(41, nameof(Content.LeafletMap.Latitude), latitude);
            builder.AddComponentParameter(42, nameof(Content.LeafletMap.Longitude), longitude);
            builder.AddComponentParameter(43, nameof(Content.LeafletMap.ReadOnly), Disabled);
            builder.AddComponentParameter(44, nameof(Content.LeafletMap.PointPicked), EventCallback.Factory.Create<Content.MapPoint>(this, point => Place(point.Latitude, point.Longitude)));
            builder.CloseComponent();
            return;
        }

        var center = new GoogleMapPosition { Lat = (double)(latitude ?? 0), Lng = (double)(longitude ?? 0) };
        builder.OpenComponent<GroveGoogleMap>(30);
        builder.AddComponentParameter(31, nameof(GroveGoogleMap.Center), center);
        builder.AddComponentParameter(32, nameof(GroveGoogleMap.Zoom), latitude is null ? 2d : 12d);
        builder.AddComponentParameter(33, nameof(GroveGoogleMap.Data), latitude is null || longitude is null
            ? Array.Empty<GroveGoogleMapMarker>()
            : new[] { new GroveGoogleMapMarker { Position = center } });
        builder.AddComponentParameter(34, nameof(GroveGoogleMap.MapClick), EventCallback.Factory.Create<GoogleMapClickEventArgs>(this, Place));
        builder.AddComponentParameter(35, nameof(GroveGoogleMap.ApiKey), apiKey);
        builder.AddComponentParameter(36, "Style", "height: 18rem; width: 100%;");
        builder.CloseComponent();
    }

    private void Place(GoogleMapClickEventArgs click)
    {
        if (click.Position is { } position)
        {
            Place((decimal)position.Lat, (decimal)position.Lng);
        }
    }

    private void Place(decimal latitude, decimal longitude)
    {
        if (Disabled || Context is null)
        {
            return;
        }

        var field = Context.GetOrCreate(FieldSetName, FieldName);
        field["Latitude"] = Math.Round(latitude, 6);
        field["Longitude"] = Math.Round(longitude, 6);
        Context.Touch(FieldSetName, FieldName);
    }

    private void Number(RenderTreeBuilder builder, int sequence, string property, decimal minimum, decimal maximum)
    {
        builder.OpenComponent<GroveNumeric<decimal?>>(sequence);
        builder.AddComponentParameter(sequence + 1, "Value", Coordinate(property));
        builder.AddComponentParameter(sequence + 2, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, value => Set(property, value is null ? null : JsonValue.Create(value.Value))));
        builder.AddComponentParameter(sequence + 3, "Min", minimum);
        builder.AddComponentParameter(sequence + 4, "Max", maximum);
        builder.AddComponentParameter(sequence + 5, "Step", "0.000001");
        builder.AddComponentParameter(sequence + 6, "Placeholder", property);
        builder.AddComponentParameter(sequence + 7, "Disabled", Disabled);
        builder.CloseComponent();
    }
}

/// <summary>
/// An item's culture and its translations (docs/MVC/parity.md › New components): the culture the
/// item is in, and each other culture of the site with the translation that holds it (opened in
/// its editor) or a button that creates it (Grove.Contents.Localization's
/// <c>api/grove/content-localization/{id}</c> and its <c>localize</c> action). An item not saved
/// yet has no translations.
/// </summary>
[BlazorComponent("LocalizationFieldSetEditor", DisplayName = "Translations", Category = "Editors", Icon = "translate", Edits = ["Localization"])]
public sealed class LocalizationFieldSetEditor : FieldEditorBase
{
    private static readonly JsonSerializerOptions s_web = new(JsonSerializerDefaults.Web);

    private LocalizationModel? _info;
    private bool _loaded;
    private string? _creating;
    private string? _error;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    protected override async Task OnParametersSetAsync()
    {
        if (_loaded || Context?.Http is not { } http || Context.ContentItemId is not { } id)
        {
            return;
        }

        _loaded = true;
        try
        {
            using var response = await http.GetAsync($"api/grove/content-localization/{Uri.EscapeDataString(id)}");
            _info = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<LocalizationModel>(s_web) : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _error = "The translations could not be loaded.";
        }
    }

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-localization-editor");
        builder.OpenElement(2, "p");
        builder.AddContent(3, "Culture: " + (_info?.CultureDisplayName ?? Value?["Culture"]?["Text"]?.ToString() ?? "the site's default culture"));
        builder.CloseElement();

        if (Context?.ContentItemId is null)
        {
            builder.OpenElement(4, "p");
            builder.AddContent(5, "Save the item to create its translations.");
            builder.CloseElement();
        }
        else if (_info is { } info)
        {
            builder.OpenElement(10, "ul");
            foreach (var translation in info.Translations)
            {
                builder.OpenElement(11, "li");
                builder.SetKey(translation.Culture);
                if (translation.ContentItemId is { } translated)
                {
                    builder.OpenElement(12, "a");
                    builder.AddAttribute(13, "href", $"Contents/ContentItems/{Uri.EscapeDataString(translated)}/Edit");
                    builder.AddContent(14, translation.DisplayName + (translation.IsDeleted ? " (no longer a site culture)" : string.Empty));
                    builder.CloseElement();
                }
                else
                {
                    builder.AddContent(15, translation.DisplayName + " ");
                    builder.OpenComponent<GroveButton>(16);
                    builder.AddComponentParameter(17, nameof(GroveButton.Text), "Create");
                    builder.AddComponentParameter(18, nameof(GroveButton.Icon), "add");
                    builder.AddComponentParameter(19, nameof(GroveButton.Size), ButtonSize.ExtraSmall);
                    builder.AddComponentParameter(20, nameof(GroveButton.ButtonStyle), ButtonStyle.Light);
                    builder.AddComponentParameter(21, nameof(GroveButton.Disabled), Disabled);
                    builder.AddComponentParameter(22, nameof(GroveButton.IsBusy), _creating == translation.Culture);
                    builder.AddComponentParameter(23, nameof(GroveButton.Click), EventCallback.Factory.Create<MouseEventArgs>(this, () => LocalizeAsync(translation.Culture)));
                    builder.CloseComponent();
                }

                builder.CloseElement();
            }

            builder.CloseElement();
            if (info.Translations.Count == 0)
            {
                builder.OpenElement(24, "p");
                builder.AddContent(25, "The site has no other culture.");
                builder.CloseElement();
            }
        }

        if (_error is not null)
        {
            builder.OpenElement(30, "div");
            builder.AddAttribute(31, "class", "grove-editor__error rz-color-danger");
            builder.AddContent(32, _error);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private async Task LocalizeAsync(string culture)
    {
        if (Context?.Http is not { } http || Context.ContentItemId is not { } id)
        {
            return;
        }

        _creating = culture;
        _error = null;
        try
        {
            using var response = await http.PostAsJsonAsync($"api/grove/content-localization/{Uri.EscapeDataString(id)}/localize", new { Culture = culture });
            if (response.IsSuccessStatusCode && await response.Content.ReadFromJsonAsync<LocalizedModel>(s_web) is { } created)
            {
                Navigation.NavigateTo($"Contents/ContentItems/{Uri.EscapeDataString(created.ContentItemId)}/Edit");
                return;
            }

            _error = "The translation could not be created.";
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _error = "The translation could not be created.";
        }
        finally
        {
            _creating = null;
        }
    }

    public sealed record LocalizationModel(string Culture, string CultureDisplayName, string? LocalizationSet, IReadOnlyList<TranslationModel> Translations);

    public sealed record TranslationModel(string Culture, string DisplayName, string? ContentItemId, bool IsDeleted);

    public sealed record LocalizedModel(string ContentItemId);
}

/// <summary>
/// The sitemap part (Grove.Seo.Sitemaps): whether the item overrides the sitemap's settings and,
/// when it does, whether it is left out of the sitemap, how often it changes and its priority
/// (0.0 to 1.0, stored as tenths).
/// </summary>
[BlazorComponent("SitemapFieldSetEditor", DisplayName = "Sitemap", Category = "Editors", Icon = "account_tree", Edits = ["Sitemap"])]
public sealed class SitemapFieldSetEditor : FieldEditorBase
{
    private static readonly string[] s_frequencies = ["Always", "Hourly", "Daily", "Weekly", "Monthly", "Yearly", "Never"];

    private static readonly Forms.SelectOption[] s_priorities =
        [.. Enumerable.Range(0, 11).Select(tenth => new Forms.SelectOption((tenth / 10m).ToString("0.0", CultureInfo.InvariantCulture), tenth.ToString(CultureInfo.InvariantCulture)))];

    private bool Overrides => Read("OverrideSitemapConfig.Value") is JsonValue value && value.TryGetValue<bool>(out var on) && on;

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-sitemap-part-editor");

        Labelled(builder, 10, "Override the sitemap configuration", child => Switch(child, 0, "OverrideSitemapConfig.Value"));
        if (Overrides)
        {
            Labelled(builder, 20, "Exclude from the sitemap", child => Switch(child, 0, "Exclude.Value"));
            Labelled(builder, 30, "Change frequency", child =>
            {
                child.OpenComponent<GroveDropDown<string>>(0);
                child.AddComponentParameter(1, "Data", s_frequencies);
                child.AddComponentParameter(2, "Value", Frequency());
                child.AddComponentParameter(3, "ValueChanged", EventCallback.Factory.Create<string>(this, value => Set("ChangeFrequency.Text", JsonValue.Create(value))));
                child.AddComponentParameter(4, "Disabled", Disabled);
                child.AddComponentParameter(5, "Style", "width: 100%;");
                child.CloseComponent();
            });
            Labelled(builder, 40, "Priority", child =>
            {
                child.OpenComponent<GroveDropDown<string>>(0);
                child.AddComponentParameter(1, "Data", s_priorities);
                child.AddComponentParameter(2, "TextProperty", nameof(Forms.SelectOption.Text));
                child.AddComponentParameter(3, "ValueProperty", nameof(Forms.SelectOption.Value));
                child.AddComponentParameter(4, "Value", Priority().ToString(CultureInfo.InvariantCulture));
                child.AddComponentParameter(5, "ValueChanged", EventCallback.Factory.Create<string>(this, value =>
                    Set("Priority.Value", JsonValue.Create(int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tenth) ? tenth : 5))));
                child.AddComponentParameter(6, "Disabled", Disabled);
                child.AddComponentParameter(7, "Style", "width: 100%;");
                child.CloseComponent();
            });
        }

        builder.CloseElement();
    }

    // Stored as the enum's name; an older item may hold its number.
    private string Frequency() => Read("ChangeFrequency.Text") switch
    {
        JsonValue value when value.TryGetValue<string>(out var name) => name,
        JsonValue value when value.TryGetValue<int>(out var number) => number switch
        {
            0 => "Daily",
            1 => "Hourly",
            2 => "Weekly",
            3 => "Monthly",
            4 => "Yearly",
            5 => "Always",
            6 => "Never",
            _ => "Daily",
        },
        _ => "Daily",
    };

    private int Priority() => Read("Priority.Value") is JsonValue value && value.TryGetValue<int>(out var tenth) ? Math.Clamp(tenth, 0, 10) : 5;

    private void Switch(RenderTreeBuilder builder, int sequence, string property)
    {
        builder.OpenComponent<GroveSwitch>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(GroveSwitch.Value), Read(property) is JsonValue value && value.TryGetValue<bool>(out var on) && on);
        builder.AddComponentParameter(sequence + 2, nameof(GroveSwitch.ValueChanged), EventCallback.Factory.Create<bool>(this, value => Set(property, JsonValue.Create(value))));
        builder.AddComponentParameter(sequence + 3, nameof(GroveSwitch.Disabled), Disabled);
        builder.CloseComponent();
    }

    private static void Labelled(RenderTreeBuilder builder, int sequence, string label, RenderFragment input)
    {
        builder.OpenComponent<GroveFormField>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(GroveFormField.Text), label);
        builder.AddComponentParameter(sequence + 2, nameof(GroveFormField.Style), "width: 100%;");
        builder.AddComponentParameter(sequence + 3, nameof(GroveFormField.ChildContent), input);
        builder.CloseComponent();
    }
}
