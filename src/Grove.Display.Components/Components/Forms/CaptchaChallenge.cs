using System.Net.Http.Json;
using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Grove.Display.Components.Forms;

/// <summary>
/// The captcha of the sign-in forms (a login extension section, docs/access.md): the ReCaptcha
/// widget with the tenant's public site key (<c>api/grove/recaptcha</c>), whose token it hands to
/// <see cref="ValueChanged"/> - the form sends it with its submission, where the login,
/// registration and password recovery events verify it. The same widget as <see cref="Captcha"/>,
/// outside a designed form. Renders nothing when ReCaptcha is not configured.
/// </summary>
[BlazorComponent("CaptchaChallenge", DisplayName = "Captcha challenge", Category = "Forms", Icon = "verified_user")]
public sealed class CaptchaChallenge : ComponentBase, IAsyncDisposable
{
    private ElementReference _element;
    private IJSObjectReference? _module;
    private DotNetObjectReference<CaptchaChallenge>? _self;
    private bool _unavailable;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    /// <summary>The widget's token, or null when it expired or failed.</summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (_unavailable)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-captcha");
        builder.AddElementReferenceCapture(2, element => _element = element);
        builder.CloseElement();
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
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Grove.Display.Components/js/grove-captcha.js");
            await _module.InvokeAsync<int?>("render", _element, settings.SiteKey, settings.ScriptUri, _self);
        }
        catch (Exception exception) when (exception is HttpRequestException or JSException or System.Text.Json.JsonException)
        {
            _unavailable = true;
            StateHasChanged();
        }
    }

    /// <summary>Called by the widget with each token (or null on expiry).</summary>
    [JSInvokable]
    public Task SetToken(string? token) => ValueChanged.InvokeAsync(string.IsNullOrEmpty(token) ? null : token);

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
