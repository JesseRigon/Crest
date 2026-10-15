using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Grove.Components.Forms;

/// <summary>
/// A captcha in a form (docs/widgets.md, the ReCaptcha forms feature): the ReCaptcha widget,
/// rendered with the tenant's public site key (<c>api/grove/recaptcha</c>), whose token the form
/// submits under <see cref="FormInputBase.Name"/> (<c>captcha</c> by default). The workflow the
/// form runs declares that input and checks it with the <c>Verify captcha</c> activity, the same
/// verification login and registration use. Required: a form does not send until it is solved.
/// Renders nothing when ReCaptcha is not configured.
/// </summary>
[BlazorComponent("Captcha", DisplayName = "Captcha", Category = "Forms", Icon = "verified_user")]
public sealed class Captcha : FormInputBase, IAsyncDisposable
{
    private ElementReference _element;
    private IJSObjectReference? _module;
    private DotNetObjectReference<Captcha>? _self;
    private int? _widget;
    private string? _token;
    private bool _unavailable;

    public Captcha()
    {
        Name = "captcha";
        Required = true;
    }

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    protected override JsonNode? CurrentValue => string.IsNullOrEmpty(_token) ? null : JsonValue.Create(_token);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (_unavailable)
        {
            return;
        }

        RenderField(builder, "captcha", control =>
        {
            control.OpenElement(0, "div");
            control.AddAttribute(1, "id", Id);
            control.AddAttribute(2, "class", "grove-captcha");
            control.AddElementReferenceCapture(3, element => _element = element);
            control.CloseElement();
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive || Services.GetService<HttpClient>() is not { } http)
        {
            return;
        }

        try
        {
            using var response = await http.GetAsync("api/grove/recaptcha");
            if (!response.IsSuccessStatusCode || await response.Content.ReadFromJsonAsync<CaptchaSettings>() is not { SiteKey.Length: > 0 } settings)
            {
                _unavailable = true;
                StateHasChanged();
                return;
            }

            _self = DotNetObjectReference.Create(this);
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Grove.Components/js/grove-captcha.js");
            _widget = await _module.InvokeAsync<int?>("render", _element, settings.SiteKey, settings.ScriptUri, _self);
        }
        catch (Exception exception) when (exception is HttpRequestException or JSException or System.Text.Json.JsonException)
        {
            _unavailable = true;
            StateHasChanged();
        }
    }

    /// <summary>The widget's token, or null when it expired or failed.</summary>
    [JSInvokable]
    public async Task SetToken(string? token)
    {
        _token = string.IsNullOrEmpty(token) ? null : token;
        await PublishAsync();
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _self?.Dispose();
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

    private sealed record CaptchaSettings(string? SiteKey, string? ScriptUri)
    {
        public string? ScriptUri { get; init; } = ScriptUri ?? "https://www.google.com/recaptcha/api.js";
    }
}
