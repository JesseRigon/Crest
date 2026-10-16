using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Grove.Display.Components.Primitives;
using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Display.Components.Editors;

/// <summary>
/// The editor of an embedding (docs/content-engine.md › Embedded content items): its embedded
/// content items in order, each edited through the same editor tree as a stand-alone item of its
/// type (the server's <c>api/grove/content-items/new-{type}/editor</c> tree, with the embedded
/// item's content in place of the new one's), added from the embedding's allowed types, removed
/// and, when the order is meaningful, reordered. What each nested editor publishes is written back
/// into the embedding, so the item the page saves holds the embedding as edited. A snapshot
/// embedding's frozen items show read-only, through the schema stored with them. The editor tree
/// places one per embedding (<see cref="FieldEditorBase.FieldSetName"/> is the embedding's name).
/// </summary>
[BlazorComponent("EmbeddedContentItemsEditor", DisplayName = "Embedded content items", Category = "Editors", Icon = "inventory_2")]
public sealed class EmbeddedContentItemsEditor : FieldEditorBase
{
    private readonly Dictionary<string, DisplayNode> _trees = new(StringComparer.Ordinal);
    private readonly List<Entry> _entries = [];
    private bool _loaded;
    private string? _adding;
    private string? _error;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    /// <summary>The types the embedding allows (its stereotypes expanded by the server).</summary>
    [Parameter]
    [ComponentParameter(Hidden = true)]
    public string[] AllowedTypes { get; set; } = [];

    /// <summary>The add buttons' labels, by position in <see cref="AllowedTypes"/> ("Add variant"), translated by the server.</summary>
    [Parameter]
    [ComponentParameter(Hidden = true)]
    public string[] AddLabels { get; set; } = [];

    /// <summary>Whether the order is meaningful: the items can be moved.</summary>
    [Parameter]
    [ComponentParameter(Hidden = true)]
    public bool Ordered { get; set; } = true;

    [Parameter]
    [ComponentParameter(Hidden = true)]
    public int? MaxCount { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (_loaded || Context is null)
        {
            return;
        }

        _loaded = true;
        foreach (var item in Value?["ContentItems"] as JsonArray ?? [])
        {
            if (item is JsonObject embedded && embedded["ContentType"]?.ToString() is { Length: > 0 } type)
            {
                await AddEntryAsync((JsonObject)embedded.DeepClone(), type, IsFrozen(embedded));
            }
        }
    }

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-bag-editor");
        for (var index = 0; index < _entries.Count; index++)
        {
            var position = index;
            var entry = _entries[index];
            builder.OpenElement(2, "div");
            builder.SetKey(entry.Key);
            builder.AddAttribute(3, "class", "grove-bag-editor__item");
            builder.OpenElement(4, "div");
            builder.AddAttribute(5, "class", "grove-bag-editor__bar");
            builder.OpenElement(6, "strong");
            builder.AddContent(7, entry.Item["DisplayText"]?.ToString() is { Length: > 0 } text ? text : entry.Type);
            builder.CloseElement();
            if (Ordered)
            {
                Button(builder, 8, "arrow_upward", "Move up", Disabled || position == 0, () => Move(position, -1));
                Button(builder, 9, "arrow_downward", "Move down", Disabled || position == _entries.Count - 1, () => Move(position, 1));
            }

            Button(builder, 10, "delete", "Remove", Disabled, () => Remove(position));
            builder.CloseElement();
            if (entry.Frozen)
            {
                RenderFrozen(builder, entry);
            }
            else if (entry.Tree is { } tree)
            {
                builder.OpenComponent<DisplayTreeRenderer>(11);
                builder.AddComponentParameter(12, nameof(DisplayTreeRenderer.Node), tree);
                builder.AddComponentParameter(13, nameof(DisplayTreeRenderer.State), entry.State);
                builder.CloseComponent();
            }
            else
            {
                builder.OpenElement(14, "div");
                builder.AddAttribute(15, "class", "grove-editor__error rz-color-danger");
                builder.AddContent(16, $"The editor of '{entry.Type}' could not be loaded.");
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        var allowed = AllowedTypes;
        if (allowed.Length > 0 && !Disabled && (MaxCount is not { } max || _entries.Count < max))
        {
            builder.OpenElement(20, "div");
            builder.AddAttribute(21, "class", "grove-bag-editor__add");
            for (var index = 0; index < allowed.Length; index++)
            {
                var type = allowed[index];
                builder.OpenComponent<GroveButton>(22);
                builder.SetKey(type);
                builder.AddComponentParameter(23, nameof(GroveButton.Text), index < AddLabels.Length ? AddLabels[index] : type);
                builder.AddComponentParameter(24, nameof(GroveButton.Icon), "add");
                builder.AddComponentParameter(25, nameof(GroveButton.Size), ButtonSize.Small);
                builder.AddComponentParameter(26, nameof(GroveButton.ButtonStyle), ButtonStyle.Light);
                builder.AddComponentParameter(27, nameof(GroveButton.IsBusy), _adding == type);
                builder.AddComponentParameter(28, nameof(GroveButton.Click), EventCallback.Factory.Create<MouseEventArgs>(this, () => AddAsync(type)));
                builder.CloseComponent();
            }

            builder.CloseElement();
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

    private void Button(RenderTreeBuilder builder, int sequence, string icon, string title, bool disabled, Action click)
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

    private async Task AddAsync(string type)
    {
        _adding = type;
        _error = null;
        try
        {
            // The new item: a fresh id, its type, and the parts its type's editor tree starts with.
            if (await TreeAsync(type) is { } tree)
            {
                await AddEntryAsync(EmbeddedItems.New(type, tree), type, frozen: false);
                WriteBack();
            }
            else
            {
                _error = $"A '{type}' item could not be created.";
            }
        }
        finally
        {
            _adding = null;
        }
    }

    private async Task AddEntryAsync(JsonObject item, string type, bool frozen)
    {
        var entry = new Entry(item["ContentItemId"]?.ToString() is { Length: > 0 } id ? id : Guid.NewGuid().ToString("n"), type, item, new DisplayState()) { Frozen = frozen };
        if (!frozen && await TreeAsync(type) is { } template)
        {
            entry.Tree = WithContent(template, EmbeddedItems.Parts(item));
        }

        entry.State.Changed += () => OnNestedChanged(entry);
        _entries.Add(entry);
    }

    // A nested editor published its content: it replaces the contained item in the bag.
    private void OnNestedChanged(Entry entry)
    {
        if (entry.Tree is null || entry.State.GetOutput(entry.Tree.Id, nameof(ContentEditor.ContentChanged)) is not JsonObject edited)
        {
            return;
        }

        // The editor edits the parts; the item keeps its own values (id, type).
        EmbeddedItems.ApplyParts(entry.Item, edited);
        WriteBack();
    }

    private void Move(int index, int offset)
    {
        var target = index + offset;
        if (target < 0 || target >= _entries.Count)
        {
            return;
        }

        (_entries[index], _entries[target]) = (_entries[target], _entries[index]);
        WriteBack();
    }

    private void Remove(int index)
    {
        _entries.RemoveAt(index);
        WriteBack();
    }

    private void WriteBack()
    {
        if (Context is null)
        {
            return;
        }

        // The stored schema (a snapshot embedding's) stays beside the items.
        Context.GetOrCreate(FieldSetName, null)["ContentItems"] = new JsonArray([.. _entries.Select(entry => (JsonNode)entry.Item.DeepClone())]);
        Context.Touch(FieldSetName, null);
    }

    // Whether the item was frozen by the parent's last publish (its id in the stored schema's items).
    private bool IsFrozen(JsonObject item) =>
        Value?["Schema"]?["Items"] is JsonArray frozen
        && item["ContentItemId"]?.ToString() is { Length: > 0 } id
        && frozen.Any(entry => entry?.ToString() == id);

    // A frozen item: its values as the schema stored with it names them, read-only.
    private void RenderFrozen(RenderTreeBuilder builder, Entry entry)
    {
        builder.OpenElement(40, "dl");
        builder.AddAttribute(41, "class", "grove-bag-editor__frozen");
        foreach (var fieldSet in Value?["Schema"]?["Types"]?[entry.Type]?["FieldSets"] as JsonArray ?? [])
        {
            var fieldSetName = fieldSet?["Name"]?.ToString();
            foreach (var field in fieldSet?["Fields"] as JsonArray ?? [])
            {
                var fieldName = field?["Name"]?.ToString();
                if (fieldSetName is null || fieldName is null || entry.Item[fieldSetName]?[fieldName] is not JsonObject stored)
                {
                    continue;
                }

                var label = field?["Settings"]?["ContentFieldSettings"]?["DisplayName"]?.ToString() is { Length: > 0 } displayName ? displayName : fieldName;
                builder.OpenElement(42, "dt");
                builder.AddContent(43, label);
                builder.CloseElement();
                builder.OpenElement(44, "dd");
                builder.AddContent(45, FrozenText(stored));
                builder.CloseElement();
            }
        }

        builder.CloseElement();
    }

    // A stored value as text: a scalar's value, an option list field's copied keys, else its ids.
    private static string FrozenText(JsonObject stored)
    {
        if (stored["Copied"] is JsonObject copied && copied.Count > 0)
        {
            return string.Join(", ", copied.Select(pair => pair.Value?["DisplayText"]?.ToString() ?? pair.Value?["Key"]?.ToString() ?? pair.Key));
        }

        if (stored["SelectedIds"] is JsonArray ids)
        {
            return string.Join(", ", ids.Select(id => id?.ToString()));
        }

        return (stored["Text"] ?? stored["Value"] ?? stored["Html"] ?? stored["Markdown"])?.ToString() ?? string.Empty;
    }

    private async Task<DisplayNode?> TreeAsync(string type)
    {
        if (_trees.TryGetValue(type, out var cached))
        {
            return cached;
        }

        if (Services.GetService<HttpClient>() is not { } http)
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
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>A copy of a type's editor tree whose root edits <paramref name="item"/>.</summary>
    public static DisplayNode WithContent(DisplayNode template, JsonObject item) => new()
    {
        Id = template.Id,
        Component = template.Component,
        Module = template.Module,
        Properties = new(template.Properties.Select(pair => KeyValuePair.Create(pair.Key, pair.Value?.DeepClone())), StringComparer.OrdinalIgnoreCase)
        {
            ["Content"] = item.DeepClone(),
        },
        Zones = template.Zones,
        Bindings = template.Bindings,
        Slots = template.Slots,
        Events = template.Events,
        OutputAliases = template.OutputAliases,
    };

    private sealed class Entry(string key, string type, JsonObject item, DisplayState state)
    {
        public string Key { get; } = key;

        public string Type { get; } = type;

        public JsonObject Item { get; set; } = item;

        public DisplayState State { get; } = state;

        public DisplayNode? Tree { get; set; }

        /// <summary>Frozen by the parent's last publish: read through the stored schema, not edited.</summary>
        public bool Frozen { get; init; }
    }
}
