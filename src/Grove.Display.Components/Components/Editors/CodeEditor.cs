using Grove.Display.Components.Primitives;
using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Grove.Display.Components.Editors;

/// <summary>
/// A code text editor (docs/MVC/parity.md › CodeEditor): Monaco with a language mode (HTML,
/// Liquid, JavaScript, CSS, JSON), line numbers and syntax highlighting, value in and value out.
/// The editor of the <c>code</c> property kind; the field editors switch to it when a field's
/// editor setting asks for code (<c>Monaco</c>, <c>CodeMirror</c>), and the settings form uses it
/// for code and JSON fields. Until Monaco is loaded (prerendering, no script) it is a plain
/// monospace text area.
/// </summary>
[BlazorComponent("CodeEditor", DisplayName = "Code", Category = "Editors", Icon = "code", Edits = ["code"])]
public sealed class CodeEditor : ComponentBase, IAsyncDisposable
{
    private ElementReference _element;
    private IJSObjectReference? _module;
    private IJSObjectReference? _handle;
    private DotNetObjectReference<CodeEditor>? _self;
    private bool _unavailable;
    private string? _rendered;
    private string? _renderedLanguage;
    private bool _renderedReadOnly;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Parameter]
    public string? Value { get; set; }

    /// <summary><c>html</c>, <c>liquid</c>, <c>javascript</c>, <c>css</c>, <c>json</c>, or any Monaco language id; plain text when absent.</summary>
    [Parameter]
    [ComponentParameter(Choices = ["html", "liquid", "javascript", "css", "json", "markdown", "plaintext"])]
    public string? Language { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>The editor's height (a CSS length).</summary>
    [Parameter]
    public string Height { get; set; } = "16rem";

    [Parameter]
    [ComponentOutput(DisplayName = "Changed")]
    public EventCallback<string?> Changed { get; set; }

    [Parameter]
    [ComponentParameter(Hidden = true)]
    public string? Id { get; set; }

    /// <summary>Monaco's language id for <paramref name="language"/> (Grove's Liquid registration is <c>liquid</c>).</summary>
    public static string LanguageId(string? language) => (language ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "" or "text" or "plain" => "plaintext",
        "js" => "javascript",
        "htm" => "html",
        var other => other,
    };

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (_unavailable || !RendererInfo.IsInteractive)
        {
            builder.OpenComponent<GroveTextArea>(0);
            builder.AddComponentParameter(1, nameof(GroveTextArea.Value), Value);
            builder.AddComponentParameter(2, nameof(GroveTextArea.Change), EventCallback.Factory.Create<string>(this, ChangeAsync));
            builder.AddComponentParameter(3, nameof(GroveTextArea.Disabled), Disabled);
            builder.AddComponentParameter(4, nameof(GroveTextArea.Rows), 12);
            builder.AddComponentParameter(5, nameof(GroveTextArea.Style), "width: 100%; font-family: var(--grove-font-mono, monospace);");
            builder.AddComponentParameter(6, nameof(GroveTextArea.Name), Id);
            builder.CloseComponent();
            return;
        }

        builder.OpenElement(10, "div");
        builder.AddAttribute(11, "class", "grove-code-editor");
        builder.AddAttribute(12, "id", Id);
        builder.AddAttribute(13, "data-language", LanguageId(Language));
        builder.AddAttribute(14, "style", $"height: {Height}; width: 100%; border: 1px solid var(--rz-border-color, #ccc); border-radius: 4px;");
        builder.AddElementReferenceCapture(15, element => _element = element);
        builder.CloseElement();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_unavailable || !RendererInfo.IsInteractive)
        {
            return;
        }

        try
        {
            if (_handle is null)
            {
                _self ??= DotNetObjectReference.Create(this);
                _module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Grove.Display.Components/js/grove-code-editor.js");
                _handle = await _module.InvokeAsync<IJSObjectReference>("create", _element, Value, LanguageId(Language), Disabled, _self);
                (_rendered, _renderedLanguage, _renderedReadOnly) = (Value, Language, Disabled);
            }
            else if (_rendered != Value || _renderedLanguage != Language || _renderedReadOnly != Disabled)
            {
                await _module!.InvokeVoidAsync("update", _handle, Value, LanguageId(Language), Disabled);
                (_rendered, _renderedLanguage, _renderedReadOnly) = (Value, Language, Disabled);
            }
        }
        catch (JSException)
        {
            _unavailable = true;
            StateHasChanged();
        }
    }

    /// <summary>The editor's text, after the viewer changed it.</summary>
    [JSInvokable]
    public async Task SetValue(string? value)
    {
        if (value == Value)
        {
            return;
        }

        _rendered = value;
        await ChangeAsync(value);
    }

    private async Task ChangeAsync(string? value)
    {
        Value = value;
        await Changed.InvokeAsync(value);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null && _handle is not null)
            {
                await _module.InvokeVoidAsync("dispose", _handle);
            }

            if (_handle is not null)
            {
                await _handle.DisposeAsync();
            }

            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }

        _self?.Dispose();
    }
}
