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
/// The editor of a <c>BagPart</c> (docs/blazor-display.md rework step 7): the contained items in
/// order, each edited through the same editor tree as a stand-alone item of its type (the
/// server's <c>api/grove/content-items/new-{type}/editor</c> tree, with the contained item's
/// content in place of the new one's), added from the part's allowed types, removed and
/// reordered. What each nested editor publishes is written back into the bag, so the item the
/// page saves holds the bag as edited. Replaces the MVC content cards and their
/// <c>BuildEditor</c> endpoint.
/// </summary>
[BlazorComponent("BagPartEditor", DisplayName = "Contained items", Category = "Editors", Icon = "inventory_2", Edits = ["BagPart"])]
public sealed class BagPartEditor : FieldEditorBase
{
    private readonly Dictionary<string, DisplayNode> _trees = new(StringComparer.Ordinal);
    private readonly List<Entry> _entries = [];
    private bool _loaded;
    private string? _adding;
    private string? _error;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    /// <summary>The types the part's settings allow; empty when they allow none named.</summary>
    private IReadOnlyList<string> AllowedTypes =>
        Context?.Part(PartName)?.Settings?["BagPartSettings"]?["ContainedContentTypes"] is JsonArray types
            ? [.. types.Select(type => type?.ToString()).OfType<string>().Where(type => type.Length > 0)]
            : [];

    protected override async Task OnParametersSetAsync()
    {
        if (_loaded || Context is null)
        {
            return;
        }

        _loaded = true;
        foreach (var item in Value?["ContentItems"] as JsonArray ?? [])
        {
            if (item is JsonObject contained && contained["ContentType"]?.ToString() is { Length: > 0 } type)
            {
                await AddEntryAsync((JsonObject)contained.DeepClone(), type);
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
            Button(builder, 8, "arrow_upward", "Move up", Disabled || position == 0, () => Move(position, -1));
            Button(builder, 9, "arrow_downward", "Move down", Disabled || position == _entries.Count - 1, () => Move(position, 1));
            Button(builder, 10, "delete", "Remove", Disabled, () => Remove(position));
            builder.CloseElement();
            if (entry.Tree is { } tree)
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
        if (allowed.Count > 0 && !Disabled)
        {
            builder.OpenElement(20, "div");
            builder.AddAttribute(21, "class", "grove-bag-editor__add");
            foreach (var type in allowed)
            {
                builder.OpenComponent<GroveButton>(22);
                builder.SetKey(type);
                builder.AddComponentParameter(23, nameof(GroveButton.Text), "Add " + type);
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
                await AddEntryAsync(ContainedItems.New(type, tree), type);
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

    private async Task AddEntryAsync(JsonObject item, string type)
    {
        var entry = new Entry(item["ContentItemId"]?.ToString() is { Length: > 0 } id ? id : Guid.NewGuid().ToString("n"), type, item, new DisplayState());
        if (await TreeAsync(type) is { } template)
        {
            entry.Tree = WithContent(template, ContainedItems.Parts(item));
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
        ContainedItems.ApplyParts(entry.Item, edited);
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

        Context.GetOrCreate(PartName, null)["ContentItems"] = new JsonArray([.. _entries.Select(entry => (JsonNode)entry.Item.DeepClone())]);
        Context.Touch(PartName, null);
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
    }
}
