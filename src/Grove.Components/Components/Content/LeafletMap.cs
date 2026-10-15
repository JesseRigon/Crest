using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Grove.Components.Content;

/// <summary>
/// A map over OpenStreetMap tiles with Leaflet (no API key): a marker at the point and, unless
/// read-only, a click that places the point (<see cref="PointPicked"/>). The map a location field
/// is placed on when no Google Maps key is configured.
/// </summary>
[BlazorComponent("LeafletMap", DisplayName = "Map", Category = "Media", Icon = "map")]
public sealed class LeafletMap : ComponentBase, IAsyncDisposable
{
    private ElementReference _element;
    private IJSObjectReference? _module;
    private DotNetObjectReference<LeafletMap>? _self;
    private (decimal? Latitude, decimal? Longitude) _shown;

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public decimal? Latitude { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public decimal? Longitude { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public int Zoom { get; set; } = 13;

    [Parameter]
    [ComponentParameter(Advanced = true)]
    public string Height { get; set; } = "18rem";

    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>The point a click placed (six decimals).</summary>
    [Parameter]
    public EventCallback<MapPoint> PointPicked { get; set; }

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-leaflet-map");
        builder.AddAttribute(2, "style", $"height: {Height}; width: 100%;");
        builder.AddElementReferenceCapture(3, element => _element = element);
        builder.CloseElement();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        try
        {
            if (firstRender)
            {
                _self = DotNetObjectReference.Create(this);
                _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Grove.Components/js/grove-leaflet-map.js");
                await _module.InvokeVoidAsync("create", _element, _self, Latitude, Longitude, Zoom, ReadOnly);
                _shown = (Latitude, Longitude);
            }
            else if (_module is not null && _shown != (Latitude, Longitude))
            {
                await _module.InvokeVoidAsync("update", _element, Latitude, Longitude);
                _shown = (Latitude, Longitude);
            }
        }
        catch (JSException)
        {
            // The map is a convenience: the coordinates stay editable without it.
        }
    }

    [JSInvokable]
    public Task OnPicked(decimal latitude, decimal longitude)
    {
        _shown = (latitude, longitude);
        return PointPicked.InvokeAsync(new MapPoint(latitude, longitude));
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose", _element);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _self?.Dispose();
    }
}

/// <summary>A point on a map.</summary>
public sealed record MapPoint(decimal Latitude, decimal Longitude);
