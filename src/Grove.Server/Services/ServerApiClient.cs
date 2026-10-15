using System.Net.Http.Json;
using Grove.AdminTheme.Api;
using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.DependencyInjection;

namespace Grove.Services;

/// <summary>
/// Phase 8: the server-side counterpart of the WASM AntiforgeryHandler, for the
/// SSR/InteractiveServer phases of InteractiveAuto. Admin components keep calling
/// api/grove/* over the same scoped HttpClient abstraction in every render context;
/// here the browser isn't making the request, so the current user's identity has to be
/// forwarded explicitly: the incoming request's Cookie header (Grove auth cookie +
/// antiforgery cookie + culture cookie) is captured once per scope and attached to
/// every outgoing loopback request, and the antiforgery request token is fetched
/// through the same forwarded cookie exactly like the WASM handler does - no browser
/// APIs involved.
///
/// Scope capture: IHttpContextAccessor covers both server contexts - during static SSR
/// it's the page request itself; during an InteractiveServer circuit it's the SignalR
/// connection request, which carries the same cookies. Captured lazily at first send
/// and cached for the scope's lifetime, so a long-lived circuit keeps working with the
/// cookies it connected with.
/// </summary>
public sealed class ForwardedAuthHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler, IAntiforgeryTokenStore
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private GroveAntiforgeryToken? _token;
    private string? _cookieHeader;
    private bool _cookieCaptured;

    public Uri? BaseAddress { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ForwardCookies(request);

        if (RequiresAntiforgeryToken(request))
        {
            var token = await GetTokenAsync(cancellationToken);
            request.Headers.TryAddWithoutValidation(token.HeaderName, token.RequestToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    public void Clear() => _token = null;

    private void ForwardCookies(HttpRequestMessage request)
    {
        if (!_cookieCaptured)
        {
            _cookieHeader = httpContextAccessor.HttpContext?.Request.Headers.Cookie.ToString();
            _cookieCaptured = true;
        }

        if (!string.IsNullOrEmpty(_cookieHeader) && !request.Headers.Contains("Cookie"))
        {
            request.Headers.TryAddWithoutValidation("Cookie", _cookieHeader);
        }

        // The loopback call acts for the same side and organization as the request that
        // caused it: the incoming headers when the request carried them, else the side the
        // shell selector stamped and the caller's organization.
        var context = httpContextAccessor.HttpContext;
        if (context is not null && !request.Headers.Contains(Grove.Access.AccessHeaders.Shell))
        {
            var shell = context.Request.Headers[Grove.Access.AccessHeaders.Shell].ToString();
            if (string.IsNullOrEmpty(shell))
            {
                shell = Grove.Access.AccessGate.SideOf(context).ToString().ToLowerInvariant();
            }

            request.Headers.TryAddWithoutValidation(Grove.Access.AccessHeaders.Shell, shell);

            var organization = context.Request.Headers[Grove.Access.AccessHeaders.Organization].ToString();
            if (string.IsNullOrEmpty(organization))
            {
                organization = context.RequestServices.GetService<Grove.Access.ICallerContextAccessor>()?.Current?.OrganizationId ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(organization))
            {
                request.Headers.TryAddWithoutValidation(Grove.Access.AccessHeaders.Organization, organization);
            }
        }
    }

    private async Task<GroveAntiforgeryToken> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null)
        {
            return _token;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null)
            {
                return _token;
            }

            var tokenUri = BaseAddress is null
                ? new Uri("api/grove/antiforgery/token", UriKind.Relative)
                : new Uri(BaseAddress, "api/grove/antiforgery/token");
            using var request = new HttpRequestMessage(HttpMethod.Get, tokenUri);
            ForwardCookies(request);
            using var response = await base.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            _token = await response.Content.ReadFromJsonAsync<GroveAntiforgeryToken>(cancellationToken)
                ?? throw new InvalidOperationException("Grove did not return an antiforgery token.");
            return _token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static bool RequiresAntiforgeryToken(HttpRequestMessage request) =>
        request.Method != HttpMethod.Get &&
        request.Method != HttpMethod.Head &&
        request.Method != HttpMethod.Options &&
        !string.Equals(request.RequestUri?.AbsolutePath, "/api/grove/antiforgery/token", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Server-side ICultureCookieWriter: deliberately does nothing. The culture cookie
/// is written exclusively by the browser (grove.theme.js via the WASM handler); the
/// server only reads it, through the platform's RequestLocalizationOptions pipeline
/// (Grove.Localization's one provider list). See docs/localization.md.
/// </summary>
public sealed class NoOpCultureCookieWriter : ICultureCookieWriter
{
    public void SetCultureCookieContext(CultureCookieContext? context)
    {
    }
}
