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
/// A taxonomy's term tree (docs/MVC/parity.md › New components): the terms in order and nested,
/// each edited through its term type's editor tree (as a contained item is in
/// <see cref="BagPartEditor"/>), added at the root or under a term, renamed, moved up, down, in
/// and out, and removed. The terms are the taxonomy's content (<c>TaxonomyPart.Terms</c>, a term's
/// children under its <c>Terms</c>), saved with the taxonomy; the server names the taxonomy on
/// every term it saves (<c>TermPart.TaxonomyContentItemId</c>).
/// </summary>
[BlazorComponent("TaxonomyPartEditor", DisplayName = "Taxonomy terms", Category = "Editors", Icon = "account_tree", Edits = ["TaxonomyPart"])]
public sealed class TaxonomyPartEditor : FieldEditorBase
{
    private readonly Dictionary<string, DisplayNode> _trees = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TermState> _open = new(StringComparer.Ordinal);
    private string? _error;
    private bool _adding;

    private string? TermType => Text("TermContentType");

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var terms = Terms();
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-taxonomy-editor");

        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "grove-editor__pair");
        builder.OpenComponent<GroveTextBox>(4);
        builder.AddComponentParameter(5, nameof(GroveTextBox.Value), TermType);
        builder.AddComponentParameter(6, nameof(GroveTextBox.Placeholder), "Term content type");
        builder.AddComponentParameter(7, nameof(GroveTextBox.Change), EventCallback.Factory.Create<string>(this, value => Set("TermContentType", JsonValue.Create(value?.Trim()))));
        builder.AddComponentParameter(8, nameof(GroveTextBox.Disabled), Disabled || terms.Count > 0);
        builder.CloseComponent();
        builder.CloseElement();

        builder.OpenElement(10, "ul");
        builder.AddAttribute(11, "class", "grove-taxonomy-editor__terms");
        RenderTerms(builder, terms, parent: null);
        builder.CloseElement();

        if (!Disabled)
        {
            builder.OpenComponent<GroveButton>(20);
            builder.AddComponentParameter(21, nameof(GroveButton.Text), "Add a term");
            builder.AddComponentParameter(22, nameof(GroveButton.Icon), "add");
            builder.AddComponentParameter(23, nameof(GroveButton.Size), ButtonSize.Small);
            builder.AddComponentParameter(24, nameof(GroveButton.ButtonStyle), ButtonStyle.Light);
            builder.AddComponentParameter(25, nameof(GroveButton.IsBusy), _adding);
            builder.AddComponentParameter(26, nameof(GroveButton.Disabled), string.IsNullOrWhiteSpace(TermType));
            builder.AddComponentParameter(27, nameof(GroveButton.Click), EventCallback.Factory.Create<MouseEventArgs>(this, () => AddAsync(parent: null)));
            builder.CloseComponent();
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

    private void RenderTerms(RenderTreeBuilder builder, JsonArray terms, JsonObject? parent)
    {
        for (var index = 0; index < terms.Count; index++)
        {
            if (terms[index] is not JsonObject term)
            {
                continue;
            }

            var position = index;
            var id = term["ContentItemId"]?.ToString() ?? string.Empty;
            builder.OpenElement(0, "li");
            builder.SetKey(id);
            builder.OpenElement(1, "div");
            builder.AddAttribute(2, "class", "grove-taxonomy-editor__bar");
            builder.OpenComponent<GroveTextBox>(3);
            builder.AddComponentParameter(4, nameof(GroveTextBox.Value), term["DisplayText"]?.ToString());
            builder.AddComponentParameter(5, nameof(GroveTextBox.Placeholder), "Name");
            builder.AddComponentParameter(6, nameof(GroveTextBox.Disabled), Disabled);
            builder.AddComponentParameter(7, nameof(GroveTextBox.Change), EventCallback.Factory.Create<string>(this, value => Rename(term, value)));
            builder.CloseComponent();
            Button(builder, 10, _open.ContainsKey(id) ? "expand_less" : "edit", "Edit the term", Disabled, () => ToggleAsync(term));
            Button(builder, 11, "arrow_upward", "Move up", Disabled || position == 0, () => Move(terms, position, -1, notify: true));
            Button(builder, 12, "arrow_downward", "Move down", Disabled || position == terms.Count - 1, () => Move(terms, position, 1, notify: true));
            Button(builder, 13, "format_indent_increase", "Nest under the term above", Disabled || position == 0, () => Indent(terms, position));
            Button(builder, 14, "format_indent_decrease", "Move out of its parent", Disabled || parent is null, () => Outdent(terms, position, parent!));
            Button(builder, 15, "add", "Add a term under it", Disabled || string.IsNullOrWhiteSpace(TermType), () => AddAsync(term));
            Button(builder, 16, "delete", "Remove", Disabled, () => Remove(terms, position));
            builder.CloseElement();

            if (_open.TryGetValue(id, out var state))
            {
                builder.OpenElement(20, "div");
                builder.AddAttribute(21, "class", "grove-taxonomy-editor__term");
                if (state.Tree is { } tree)
                {
                    builder.OpenComponent<DisplayTreeRenderer>(22);
                    builder.AddComponentParameter(23, nameof(DisplayTreeRenderer.Node), tree);
                    builder.AddComponentParameter(24, nameof(DisplayTreeRenderer.State), state.State);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(25, $"The editor of '{term["ContentType"]}' could not be loaded.");
                }

                builder.CloseElement();
            }

            if (term["Terms"] is JsonArray { Count: > 0 } children)
            {
                builder.OpenElement(30, "ul");
                RenderTerms(builder, children, term);
                builder.CloseElement();
            }

            builder.CloseElement();
        }
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

    private void Button(RenderTreeBuilder builder, int sequence, string icon, string title, bool disabled, Action click) =>
        Button(builder, sequence, icon, title, disabled, () => { click(); return Task.CompletedTask; });

    private JsonArray Terms()
    {
        var part = Context!.GetOrCreate(PartName, null);
        if (part["Terms"] is not JsonArray terms)
        {
            part["Terms"] = terms = [];
        }

        return terms;
    }

    private void Changed() => Context?.Touch(PartName, null);

    private void Rename(JsonObject term, string? name)
    {
        term["DisplayText"] = name?.Trim();
        if (term["TitlePart"] is JsonObject title)
        {
            title["Title"] = name?.Trim();
        }

        Changed();
    }

    private static void Move(JsonArray terms, int index, int offset)
    {
        var target = index + offset;
        if (target < 0 || target >= terms.Count)
        {
            return;
        }

        var term = terms[index];
        terms.RemoveAt(index);
        terms.Insert(target, term);
    }

    private void Move(JsonArray terms, int index, int offset, bool notify)
    {
        Move(terms, index, offset);
        if (notify)
        {
            Changed();
        }
    }

    private void Indent(JsonArray terms, int index)
    {
        if (index == 0 || terms[index - 1] is not JsonObject above)
        {
            return;
        }

        var term = terms[index];
        terms.RemoveAt(index);
        if (above["Terms"] is not JsonArray children)
        {
            above["Terms"] = children = [];
        }

        children.Add(term);
        Changed();
    }

    private void Outdent(JsonArray terms, int index, JsonObject parent)
    {
        // The parent's own list holds the parent; the term goes right after it.
        var grand = FindContainer(Terms(), parent);
        if (grand is null)
        {
            return;
        }

        var term = terms[index];
        terms.RemoveAt(index);
        var at = grand.IndexOf(parent);
        grand.Insert(at + 1, term);
        if (terms.Count == 0)
        {
            parent.Remove("Terms");
        }

        Changed();
    }

    private static JsonArray? FindContainer(JsonArray terms, JsonObject term)
    {
        if (terms.Contains(term))
        {
            return terms;
        }

        foreach (var child in terms.OfType<JsonObject>())
        {
            if (child["Terms"] is JsonArray children && FindContainer(children, term) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void Remove(JsonArray terms, int index)
    {
        if (terms[index] is JsonObject term && term["ContentItemId"]?.ToString() is { } id)
        {
            _open.Remove(id);
        }

        terms.RemoveAt(index);
        Changed();
    }

    private async Task AddAsync(JsonObject? parent)
    {
        if (TermType is not { Length: > 0 } type)
        {
            return;
        }

        _adding = true;
        _error = null;
        try
        {
            var template = await TreeAsync(type);
            var term = ContainedItems.New(type, template);
            term["DisplayText"] = "New term";
            if (parent is null)
            {
                Terms().Add(term);
            }
            else
            {
                if (parent["Terms"] is not JsonArray children)
                {
                    parent["Terms"] = children = [];
                }

                children.Add(term);
            }

            Changed();
            await ToggleAsync(term);
        }
        finally
        {
            _adding = false;
        }
    }

    private async Task ToggleAsync(JsonObject term)
    {
        var id = term["ContentItemId"]?.ToString() ?? string.Empty;
        if (_open.Remove(id))
        {
            return;
        }

        var type = term["ContentType"]?.ToString() ?? TermType ?? string.Empty;
        var state = new TermState(new DisplayState());
        if (await TreeAsync(type) is { } template)
        {
            state.Tree = BagPartEditor.WithContent(template, ContainedItems.Parts(term));
        }

        state.State.Changed += () => OnTermChanged(term, state);
        _open[id] = state;
    }

    // A term's editor published its parts: they replace the term's, its own values (id, name,
    // children) are kept, and a title names the term.
    private void OnTermChanged(JsonObject term, TermState state)
    {
        if (state.Tree is null || state.State.GetOutput(state.Tree.Id, nameof(ContentEditor.ContentChanged)) is not JsonObject edited)
        {
            return;
        }

        ContainedItems.ApplyParts(term, edited);
        if (edited["TitlePart"]?["Title"]?.ToString() is { Length: > 0 } title)
        {
            term["DisplayText"] = title;
        }

        Changed();
    }

    private async Task<DisplayNode?> TreeAsync(string type)
    {
        if (_trees.TryGetValue(type, out var cached))
        {
            return cached;
        }

        if (Context?.Http is not { } http)
        {
            return null;
        }

        try
        {
            var tree = await http.GetFromJsonAsync<DisplayTree>($"api/grove/content-items/new-{Uri.EscapeDataString(type)}/editor");
            if (tree?.Root is { } root)
            {
                _trees[type] = root;
            }

            return tree?.Root;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _error = $"The editor of '{type}' could not be loaded.";
            return null;
        }
    }

    private sealed class TermState(DisplayState state)
    {
        public DisplayState State { get; } = state;

        public DisplayNode? Tree { get; set; }
    }
}

/// <summary>
/// A taxonomy field's terms (docs/MVC/parity.md › New components): the taxonomy's term tree with
/// a check box per term (a radio when the field takes one term; only leaves when the settings say
/// so), or, with the <c>Tags</c> editor, the terms as a list to pick from with the creation of a
/// new tag by name when the field is open. The terms come from Grove.Contents.Taxonomies'
/// <c>api/grove/taxonomies/{id}/terms</c>; the field stores the taxonomy's id, the selected term
/// ids and, for tags, their names.
/// </summary>
[BlazorComponent("TaxonomyFieldEditor", DisplayName = "Taxonomy terms", Category = "Editors", Icon = "sell", Edits = ["TaxonomyField"])]
public sealed class TaxonomyFieldEditor : FieldEditorBase
{
    private static readonly JsonSerializerOptions s_web = new(JsonSerializerDefaults.Web);

    private TaxonomyTermsModel? _taxonomy;
    private bool _loaded;
    private string? _error;
    private string? _newTag;
    private bool _creating;

    private string? TaxonomyId => Setting("TaxonomyFieldSettings", "TaxonomyContentItemId")?.ToString();

    private bool Unique => Setting("TaxonomyFieldSettings", "Unique") is JsonValue value && value.TryGetValue<bool>(out var unique) && unique;

    private bool LeavesOnly => Setting("TaxonomyFieldSettings", "LeavesOnly") is JsonValue value && value.TryGetValue<bool>(out var leaves) && leaves;

    private bool Open => IsMode("Tags")
        ? !(Setting("TaxonomyFieldTagsEditorSettings", "Open") is JsonValue tags && tags.TryGetValue<bool>(out var openTags) && !openTags)
        : Setting("TaxonomyFieldSettings", "Open") is JsonValue value && value.TryGetValue<bool>(out var open) && open;

    private IReadOnlyList<string> Selected => Value?["TermContentItemIds"] is JsonArray ids
        ? [.. ids.Select(id => id?.ToString()).OfType<string>()]
        : [];

    protected override async Task OnParametersSetAsync()
    {
        if (_loaded || Context?.Http is not { } http || string.IsNullOrEmpty(TaxonomyId))
        {
            return;
        }

        _loaded = true;
        try
        {
            using var response = await http.GetAsync($"api/grove/taxonomies/{Uri.EscapeDataString(TaxonomyId)}/terms");
            if (response.IsSuccessStatusCode)
            {
                _taxonomy = await response.Content.ReadFromJsonAsync<TaxonomyTermsModel>(s_web);
            }
            else
            {
                _error = "The taxonomy's terms could not be loaded.";
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _error = "The taxonomy's terms could not be loaded.";
        }
    }

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        if (string.IsNullOrEmpty(TaxonomyId))
        {
            builder.AddContent(0, "The field's settings name no taxonomy.");
            return;
        }

        if (_error is not null)
        {
            builder.OpenElement(1, "div");
            builder.AddAttribute(2, "class", "grove-editor__error rz-color-danger");
            builder.AddContent(3, _error);
            builder.CloseElement();
        }

        if (_taxonomy is null)
        {
            return;
        }

        if (IsMode("Tags"))
        {
            RenderTags(builder);
            return;
        }

        builder.OpenElement(10, "ul");
        builder.AddAttribute(11, "class", "grove-taxonomy-field");
        RenderTerms(builder, _taxonomy.Terms);
        builder.CloseElement();
    }

    private void RenderTerms(RenderTreeBuilder builder, IReadOnlyList<TaxonomyTermModel> terms)
    {
        var selected = Selected;
        foreach (var term in terms)
        {
            var selectable = !LeavesOnly || term.Children.Count == 0;
            builder.OpenElement(0, "li");
            builder.SetKey(term.ContentItemId);
            builder.OpenElement(1, "label");
            if (selectable)
            {
                builder.OpenElement(2, "input");
                builder.AddAttribute(3, "type", Unique ? "radio" : "checkbox");
                builder.AddAttribute(4, "name", Id);
                builder.AddAttribute(5, "checked", selected.Contains(term.ContentItemId));
                builder.AddAttribute(6, "disabled", Disabled);
                builder.AddAttribute(7, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, change => Toggle(term.ContentItemId, change.Value is true || change.Value is string text && (text == "on" || text == "true"))));
                builder.CloseElement();
            }

            builder.AddContent(8, " " + term.DisplayText);
            builder.CloseElement();
            if (term.Children.Count > 0)
            {
                builder.OpenElement(9, "ul");
                RenderTerms(builder, term.Children);
                builder.CloseElement();
            }

            builder.CloseElement();
        }
    }

    private void Toggle(string termId, bool on)
    {
        var selected = Unique ? [] : Selected.ToList();
        selected.Remove(termId);
        if (on)
        {
            selected.Add(termId);
        }

        Write(selected);
    }

    private void RenderTags(RenderTreeBuilder builder)
    {
        var options = Flatten(_taxonomy!.Terms).Where(term => !LeavesOnly || term.Children.Count == 0).ToList();
        builder.OpenComponent<GroveDropDown<IEnumerable<string>>>(20);
        builder.AddComponentParameter(21, "Data", options);
        builder.AddComponentParameter(22, "TextProperty", nameof(TaxonomyTermModel.DisplayText));
        builder.AddComponentParameter(23, "ValueProperty", nameof(TaxonomyTermModel.ContentItemId));
        builder.AddComponentParameter(24, "Multiple", !Unique);
        builder.AddComponentParameter(25, "Chips", true);
        builder.AddComponentParameter(26, "AllowFiltering", true);
        builder.AddComponentParameter(27, "Value", (IEnumerable<string>)Selected);
        builder.AddComponentParameter(28, "ValueChanged", EventCallback.Factory.Create<IEnumerable<string>>(this, ids => Write([.. ids ?? []])));
        builder.AddComponentParameter(29, "Disabled", Disabled);
        builder.AddComponentParameter(30, "Placeholder", Setting("TaxonomyFieldSettings", "Placeholder")?.ToString());
        builder.AddComponentParameter(31, "Style", "width: 100%;");
        builder.CloseComponent();

        if (!Open || Disabled)
        {
            return;
        }

        builder.OpenElement(40, "div");
        builder.AddAttribute(41, "class", "grove-editor__pair");
        builder.OpenComponent<GroveTextBox>(42);
        builder.AddComponentParameter(43, nameof(GroveTextBox.Value), _newTag);
        builder.AddComponentParameter(44, nameof(GroveTextBox.Placeholder), "New tag");
        builder.AddComponentParameter(45, nameof(GroveTextBox.Change), EventCallback.Factory.Create<string>(this, value => _newTag = value));
        builder.CloseComponent();
        builder.OpenComponent<GroveButton>(46);
        builder.AddComponentParameter(47, nameof(GroveButton.Text), "Add tag");
        builder.AddComponentParameter(48, nameof(GroveButton.Icon), "add");
        builder.AddComponentParameter(49, nameof(GroveButton.Size), ButtonSize.Small);
        builder.AddComponentParameter(50, nameof(GroveButton.ButtonStyle), ButtonStyle.Light);
        builder.AddComponentParameter(51, nameof(GroveButton.IsBusy), _creating);
        builder.AddComponentParameter(52, nameof(GroveButton.Click), EventCallback.Factory.Create<MouseEventArgs>(this, CreateTagAsync));
        builder.CloseComponent();
        builder.CloseElement();
    }

    private async Task CreateTagAsync()
    {
        if (string.IsNullOrWhiteSpace(_newTag) || Context?.Http is not { } http || TaxonomyId is null)
        {
            return;
        }

        // A tag of that name already in the taxonomy is picked, not created again.
        if (Flatten(_taxonomy!.Terms).FirstOrDefault(term => string.Equals(term.DisplayText, _newTag.Trim(), StringComparison.OrdinalIgnoreCase)) is { } existing)
        {
            Write(Unique ? [existing.ContentItemId] : [.. Selected.Where(id => id != existing.ContentItemId), existing.ContentItemId]);
            _newTag = null;
            return;
        }

        _creating = true;
        try
        {
            using var response = await http.PostAsJsonAsync($"api/grove/taxonomies/{Uri.EscapeDataString(TaxonomyId)}/tags", new { DisplayText = _newTag.Trim() });
            if (response.IsSuccessStatusCode && await response.Content.ReadFromJsonAsync<TaxonomyTermModel>(s_web) is { } created)
            {
                _taxonomy = _taxonomy! with { Terms = [.. _taxonomy.Terms, created] };
                Write(Unique ? [created.ContentItemId] : [.. Selected, created.ContentItemId]);
                _newTag = null;
                _error = null;
            }
            else
            {
                _error = "The tag could not be created.";
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _error = "The tag could not be created.";
        }
        finally
        {
            _creating = false;
        }
    }

    // The field names its taxonomy, and a tags field keeps its terms' names for its display.
    private void Write(IReadOnlyList<string> ids)
    {
        var field = Context!.GetOrCreate(PartName, FieldName);
        field["TaxonomyContentItemId"] = TaxonomyId;
        field["TermContentItemIds"] = new JsonArray([.. ids.Select(id => (JsonNode?)JsonValue.Create(id))]);
        if (IsMode("Tags"))
        {
            var names = Flatten(_taxonomy?.Terms ?? []).ToDictionary(term => term.ContentItemId, term => term.DisplayText, StringComparer.Ordinal);
            field["TagNames"] = new JsonArray([.. ids.Where(names.ContainsKey).Select(id => (JsonNode?)JsonValue.Create(names[id]))]);
        }

        Context.Touch(PartName, FieldName);
    }

    private static IEnumerable<TaxonomyTermModel> Flatten(IEnumerable<TaxonomyTermModel> terms) =>
        terms.SelectMany(term => new[] { term }.Concat(Flatten(term.Children)));

    /// <summary>A taxonomy's terms as the taxonomies API serves them.</summary>
    public sealed record TaxonomyTermsModel(string ContentItemId, string? DisplayText, string? TermContentType, IReadOnlyList<TaxonomyTermModel> Terms);

    /// <summary>A term and its children.</summary>
    public sealed record TaxonomyTermModel(string ContentItemId, string DisplayText, IReadOnlyList<TaxonomyTermModel> Children);
}

/// <summary>
/// Items held inside another item's content (a bag's items, a taxonomy's terms): a new one from a
/// type's editor tree, and the split between the item's own values and its parts.
/// </summary>
public static class ContainedItems
{
    private static readonly HashSet<string> s_itemProperties = new(StringComparer.Ordinal)
    {
        "ContentItemId", "ContentItemVersionId", "ContentType", "DisplayText", "Latest", "Published",
        "ModifiedUtc", "PublishedUtc", "CreatedUtc", "Owner", "Author", "Terms",
    };

    /// <summary>A new item of <paramref name="type"/>: a fresh id, its type, and the parts the type's editor tree starts with.</summary>
    public static JsonObject New(string type, DisplayNode? editorTree)
    {
        var item = editorTree?.Properties.TryGetValue("Content", out var content) == true && content is JsonObject parts
            ? (JsonObject)parts.DeepClone()
            : [];
        item["ContentItemId"] = NewId();
        item["ContentItemVersionId"] = NewId();
        item["ContentType"] = type;
        item["Latest"] = true;
        item["Published"] = true;
        return item;
    }

    /// <summary>A new item id: 26 lowercase alphanumerics, as the server's ids are.</summary>
    public static string NewId() => Guid.NewGuid().ToString("n")[..26];

    /// <summary>The item's parts, without its own values: what its editor tree edits.</summary>
    public static JsonObject Parts(JsonObject item) =>
        new([.. item.Where(pair => !s_itemProperties.Contains(pair.Key)).Select(pair => KeyValuePair.Create(pair.Key, pair.Value?.DeepClone()))]);

    /// <summary>Replaces the item's parts with <paramref name="edited"/>'s, keeping its own values.</summary>
    public static void ApplyParts(JsonObject item, JsonObject edited)
    {
        foreach (var name in item.Select(pair => pair.Key).Where(name => !s_itemProperties.Contains(name)).ToArray())
        {
            item.Remove(name);
        }

        foreach (var (name, value) in edited)
        {
            if (!s_itemProperties.Contains(name))
            {
                item[name] = value?.DeepClone();
            }
        }
    }
}
