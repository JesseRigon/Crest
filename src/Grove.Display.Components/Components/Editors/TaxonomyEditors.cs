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
/// A taxonomy's own value: the type its terms are created as (its terms are edited by
/// <see cref="TaxonomyTermsEditor"/>, on the taxonomy's Terms embedding). Fixed once the
/// taxonomy has terms.
/// </summary>
[BlazorComponent("TaxonomyFieldSetEditor", DisplayName = "Category term type", Category = "Editors", Icon = "account_tree", Edits = ["Taxonomy"])]
public sealed class TaxonomyFieldSetEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var hasTerms = Context?.Get("Terms", null)?["ContentItems"] is JsonArray { Count: > 0 };
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-editor__pair");
        builder.OpenComponent<GroveTextBox>(2);
        builder.AddComponentParameter(3, nameof(GroveTextBox.Value), Text("TermContentType.Text"));
        builder.AddComponentParameter(4, nameof(GroveTextBox.Placeholder), "Term content type");
        builder.AddComponentParameter(5, nameof(GroveTextBox.Change), EventCallback.Factory.Create<string>(this, value => Set("TermContentType.Text", JsonValue.Create(value?.Trim()))));
        builder.AddComponentParameter(6, nameof(GroveTextBox.Disabled), Disabled || hasTerms);
        builder.CloseComponent();
        builder.CloseElement();
    }
}

/// <summary>
/// A taxonomy's term tree (docs/MVC/parity.md › New components): the terms in order and nested,
/// each edited through its term type's editor tree (as an embedded item is in
/// <see cref="EmbeddedContentItemsEditor"/>), added at the root or under a term, renamed, moved up,
/// down, in and out, and removed. The terms are the taxonomy's nested <c>Terms</c> embedding (a
/// term's children under its own <c>Terms</c>), saved with the taxonomy; the server names the
/// taxonomy on every term it saves (<c>TermFieldSet.TaxonomyContentItemId</c>). The editor tree
/// places it for the embedding in place of the generic embedding editor.
/// </summary>
[BlazorComponent("TaxonomyTermsEditor", DisplayName = "Category terms", Category = "Editors", Icon = "account_tree", Edits = ["Taxonomy.Terms"])]
public sealed class TaxonomyTermsEditor : FieldEditorBase
{
    private readonly Dictionary<string, DisplayNode> _trees = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TermState> _open = new(StringComparer.Ordinal);
    private string? _error;
    private bool _adding;

    // The type the taxonomy names for its terms (its Taxonomy field set).
    private string? TermType => Context?.Get("Taxonomy", null)?["TermContentType"]?["Text"]?.ToString();

    // A term's children: under its own Terms embedding.
    private static JsonArray? Children(JsonObject term) => term["Terms"]?["ContentItems"] as JsonArray;

    private static JsonArray ChildrenOrCreate(JsonObject term)
    {
        if (term["Terms"] is not JsonObject embedding)
        {
            term["Terms"] = embedding = [];
        }

        if (embedding["ContentItems"] is not JsonArray children)
        {
            embedding["ContentItems"] = children = [];
        }

        return children;
    }

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var terms = Terms();
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-taxonomy-editor");

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

            if (Children(term) is { Count: > 0 } children)
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
        var embedding = Context!.GetOrCreate(FieldSetName, null);
        if (embedding["ContentItems"] is not JsonArray terms)
        {
            embedding["ContentItems"] = terms = [];
        }

        return terms;
    }

    private void Changed() => Context?.Touch(FieldSetName, null);

    private void Rename(JsonObject term, string? name)
    {
        term["DisplayText"] = name?.Trim();
        // A term's title is its Title field set's Title text field.
        if (term["Title"] is JsonObject title)
        {
            title["Title"] = new JsonObject { ["Text"] = name?.Trim() };
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
        ChildrenOrCreate(above).Add(term);
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
            if (Children(child) is { } children && FindContainer(children, term) is { } found)
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
            var term = EmbeddedItems.New(type, template);
            term["DisplayText"] = "New term";
            if (parent is null)
            {
                Terms().Add(term);
            }
            else
            {
                ChildrenOrCreate(parent).Add(term);
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
            state.Tree = EmbeddedContentItemsEditor.WithContent(template, EmbeddedItems.Parts(term));
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

        EmbeddedItems.ApplyParts(term, edited);
        if (edited["Title"]?["Title"]?["Text"]?.ToString() is { Length: > 0 } title)
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
/// Items held inside another item's content (an embedding's items, a taxonomy's terms): a new one
/// from a type's editor tree, and the split between the item's own values and its parts.
/// </summary>
public static class EmbeddedItems
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
