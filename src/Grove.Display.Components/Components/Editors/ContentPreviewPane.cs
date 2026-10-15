using System.Net;
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
/// The editor's live preview (docs/MVC/parity.md › New components): the item as edited, unsaved,
/// rendered by Grove.Contents.Preview's <c>api/grove/content-preview</c> the way its display renders,
/// shown beside the editor tree and refreshed a moment after the item stops changing. Without the
/// Content Preview feature (the API answers 404) the pane says so.
/// </summary>
[BlazorComponent("ContentPreviewPane", DisplayName = "Content preview", Category = "Editors", Icon = "preview")]
public sealed class ContentPreviewPane : ComponentBase, IDisposable
{
    private static readonly TimeSpan s_quiet = TimeSpan.FromMilliseconds(800);

    private CancellationTokenSource? _pending;
    private string? _html;
    private string? _message;
    private bool _busy;
    private string? _rendered;

    [Parameter]
    public string? ContentType { get; set; }

    [Parameter]
    public string? ContentItemId { get; set; }

    [Parameter]
    public string? DisplayText { get; set; }

    /// <summary>The item's content as the editor holds it.</summary>
    [Parameter]
    public JsonNode? Content { get; set; }

    /// <summary>The display type to render (<c>Detail</c> by default).</summary>
    [Parameter]
    public string? DisplayType { get; set; }

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    protected override void OnParametersSet()
    {
        if (!RendererInfo.IsInteractive || string.IsNullOrEmpty(ContentType))
        {
            return;
        }

        var signature = ContentType + "|" + DisplayText + "|" + DisplayType + "|" + Content?.ToJsonString();
        if (signature == _rendered)
        {
            return;
        }

        _rendered = signature;
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = new CancellationTokenSource();
        _ = RefreshLaterAsync(_pending.Token);
    }

    private async Task RefreshLaterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(s_quiet, cancellationToken);
            await InvokeAsync(() => RefreshAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (Services.GetService<HttpClient>() is not { } http || string.IsNullOrEmpty(ContentType))
        {
            return;
        }

        _busy = true;
        StateHasChanged();
        try
        {
            using var response = await http.PostAsJsonAsync("api/grove/content-preview", new
            {
                ContentType,
                ContentItemId,
                DisplayText,
                Content = Content as JsonObject,
                DisplayType,
            }, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _html = await response.Content.ReadAsStringAsync(cancellationToken);
                _message = null;
            }
            else
            {
                _message = response.StatusCode == HttpStatusCode.NotFound
                    ? "The preview is not available (the Content Preview feature is off, or the item cannot be displayed)."
                    : response.StatusCode == HttpStatusCode.Forbidden
                        ? "You may not preview this item."
                        : "The preview could not be rendered.";
            }
        }
        catch (HttpRequestException)
        {
            _message = "The preview could not be rendered.";
        }
        finally
        {
            _busy = false;
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-content-preview");
        builder.AddAttribute(2, "data-testid", "content-preview-pane");
        builder.OpenElement(3, "div");
        builder.AddAttribute(4, "class", "grove-content-preview__bar");
        builder.OpenComponent<GroveButton>(5);
        builder.AddComponentParameter(6, nameof(GroveButton.Text), "Refresh");
        builder.AddComponentParameter(7, nameof(GroveButton.Icon), "refresh");
        builder.AddComponentParameter(8, nameof(GroveButton.Size), ButtonSize.Small);
        builder.AddComponentParameter(9, nameof(GroveButton.ButtonStyle), ButtonStyle.Light);
        builder.AddComponentParameter(10, nameof(GroveButton.IsBusy), _busy);
        builder.AddComponentParameter(11, nameof(GroveButton.Click), EventCallback.Factory.Create<MouseEventArgs>(this, () => RefreshAsync()));
        builder.CloseComponent();
        builder.CloseElement();

        if (_message is not null)
        {
            builder.OpenComponent<GroveAlert>(20);
            builder.AddComponentParameter(21, nameof(GroveAlert.AlertStyle), AlertStyle.Info);
            builder.AddComponentParameter(22, nameof(GroveAlert.AllowClose), false);
            builder.AddComponentParameter(23, nameof(GroveAlert.Text), _message);
            builder.CloseComponent();
        }

        // The rendered display in a sandboxed frame: its markup never reaches the editor's page.
        builder.OpenElement(30, "iframe");
        builder.AddAttribute(31, "class", "grove-content-preview__frame");
        builder.AddAttribute(32, "title", "Preview");
        builder.AddAttribute(33, "sandbox", string.Empty);
        builder.AddAttribute(34, "srcdoc", _html ?? string.Empty);
        builder.AddAttribute(35, "style", "width: 100%; min-height: 32rem; border: 1px solid var(--rz-base-300, #ddd);");
        builder.CloseElement();
        builder.CloseElement();
    }

    public void Dispose()
    {
        _pending?.Cancel();
        _pending?.Dispose();
    }
}
