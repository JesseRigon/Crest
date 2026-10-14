using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Crest.Facebook.Client;

/// <summary>The Facebook social plugins the retired widgets embedded.</summary>
public enum FacebookPluginKind
{
    Chat,
    Comments,
    ContinueWith,
    Like,
    Quote,
    Save,
    Share,
}

/// <summary>
/// One Facebook social plugin (docs/widgets.md, the Facebook widgets): the plugin's markup, then
/// the SDK loaded by this module's own script (the tenant's app id and version from the module's
/// SDK endpoints) and the element parsed. Module code, so the scripting ruling allows it; a
/// template only names the kind and its options.
/// </summary>
[BlazorComponent("FacebookPlugin", DisplayName = "Facebook plugin", Category = "Social", Icon = "thumb_up")]
public sealed class FacebookPlugin : ComponentBase, IAsyncDisposable
{
    private ElementReference _element;
    private IJSObjectReference? _module;

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public FacebookPluginKind Kind { get; set; } = FacebookPluginKind.Like;

    /// <summary>The page the plugin is about (like, share, comments, save, quote); the current page when empty.</summary>
    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Href { get; set; }

    /// <summary>The Facebook page id a chat plugin talks for.</summary>
    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? PageId { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content", Choices = ["standard", "button_count", "button", "box_count"])]
    public string? Layout { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content", Choices = ["small", "medium", "large"])]
    public string? Size { get; set; }

    [Parameter]
    public int? Width { get; set; }

    /// <summary>How many comments a comments plugin shows.</summary>
    [Parameter]
    public int? NumberOfPosts { get; set; }

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "crest-facebook-plugin");
        builder.AddElementReferenceCapture(2, element => _element = element);
        builder.OpenElement(3, "div");
        foreach (var (name, value) in Attributes())
        {
            builder.AddAttribute(4, name, value);
        }

        builder.CloseElement();
        builder.CloseElement();
    }

    /// <summary>The plugin element's attributes: its class and the data attributes the SDK reads.</summary>
    public IEnumerable<(string Name, string Value)> Attributes()
    {
        var href = Components.Content.SafeUrl.IsSafe(Href) ? Href : null;
        IEnumerable<(string, string?)> attributes = Kind switch
        {
            FacebookPluginKind.Chat => [("class", "fb-customerchat"), ("page_id", PageId), ("attribution", "setup_tool")],
            FacebookPluginKind.Comments => [("class", "fb-comments"), ("data-href", href), ("data-width", Width?.ToString()), ("data-numposts", NumberOfPosts?.ToString())],
            FacebookPluginKind.ContinueWith => [("class", "fb-login-button"), ("data-button-type", "continue_with"), ("data-size", Size ?? "large"), ("data-width", Width?.ToString()), ("data-layout", "default")],
            FacebookPluginKind.Like => [("class", "fb-like"), ("data-href", href), ("data-layout", Layout ?? "standard"), ("data-action", "like"), ("data-size", Size ?? "small"), ("data-share", "false")],
            FacebookPluginKind.Quote => [("class", "fb-quote"), ("data-href", href)],
            FacebookPluginKind.Save => [("class", "fb-save"), ("data-uri", href), ("data-size", Size ?? "small")],
            FacebookPluginKind.Share => [("class", "fb-share-button"), ("data-href", href), ("data-layout", Layout ?? "button_count"), ("data-size", Size ?? "small")],
            _ => [],
        };

        return attributes.Where(attribute => !string.IsNullOrEmpty(attribute.Item2)).Select(attribute => (attribute.Item1, attribute.Item2!));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        try
        {
            _module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Crest.Facebook.BlazorWasm/facebook-plugin.js");
            await _module.InvokeVoidAsync("parse", _element, System.Globalization.CultureInfo.CurrentUICulture.Name);
        }
        catch (JSException)
        {
            // The SDK is the visitor's network's to block; the page stays usable without it.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
