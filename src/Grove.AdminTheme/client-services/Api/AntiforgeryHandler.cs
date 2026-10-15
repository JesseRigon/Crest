using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;

namespace Grove.AdminTheme.Api;

/// <summary>
/// Adds Grove's antiforgery request token to every unsafe same-origin Grove
/// request. Authentication continues to use the browser's Grove cookie.
/// </summary>
public sealed class AntiforgeryHandler(IJSInProcessRuntime js, Grove.Components.Modules.GroveShellContext shell) : DelegatingHandler, IAntiforgeryTokenStore, ICultureCookieWriter
{
    public const string ShellHeader = "X-Shell";

    public const string OrganizationHeader = "X-Org";

    /// <summary>Where Grove hands out the antiforgery request token (Grove.Server's endpoint).</summary>
    public const string TokenPath = "api/grove/antiforgery/token";

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private GroveAntiforgeryToken? _token;
    private CultureCookieContext? _cultureCookieContext;

    // Set by Program.cs to the same document-base address the owning HttpClient
    // itself uses (see apiBaseAddress - tenantPrefix + shellBase; the server
    // middleware strips the shell base off "{shellBase}/api/..." so these resolve to
    // the tenant-root api surface). GetTokenAsync below sends its own request
    // directly through the inner handler chain via base.SendAsync, bypassing the
    // owning HttpClient.SendAsync's usual BaseAddress + relative-URI combination
    // step, so it needs the base handed to it explicitly.
    public Uri? BaseAddress { get; set; }

    // Pushed by AdminShellState.RefreshManifestAsync every time it resolves culture (see
    // docs/localization.md phase 15). The cookie is per-origin but a session
    // override lives in sessionStorage (per-tab) - so between two open tabs with
    // different overrides, whichever tab last wrote the cookie "wins" for both until the
    // other tab makes its own request. Re-resolving and rewriting the cookie immediately
    // before every outgoing Grove API request (not only on manifest refresh) means each
    // tab's own requests always carry its own resolved culture, regardless of what the
    // other tab last wrote.
    public void SetCultureCookieContext(CultureCookieContext? context) => _cultureCookieContext = context;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        // The side and organization this request acts in; the server never defaults them.
        request.Headers.TryAddWithoutValidation(ShellHeader, shell.Shell);
        if (shell.OrganizationId is { Length: > 0 } organizationId)
        {
            request.Headers.TryAddWithoutValidation(OrganizationHeader, organizationId);
        }

        RewriteCultureCookie();

        if (RequiresAntiforgeryToken(request))
        {
            var token = await GetTokenAsync(cancellationToken);
            request.Headers.TryAddWithoutValidation(token.HeaderName, token.RequestToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    // Synchronous JS interop (setAdminCulture/getSessionCultureOverride/getBrowserLocale
    // in grove.theme.js are all plain, non-async functions) so this can run inline on the
    // request path with no extra await hop. Never throws/blocks a request on failure - a
    // culture-cookie write is an enhancement, not something any Grove API call should ever
    // fail over.
    private void RewriteCultureCookie()
    {
        var context = _cultureCookieContext;
        if (context is null)
        {
            return;
        }

        try
        {
            var sessionOverride = js.Invoke<string?>("groveTheme.getSessionCultureOverride", context.UserName);
            var browserLocale = js.Invoke<string?>("groveTheme.getBrowserLocale");
            var resolved = Grove.AdminTheme.State.AdminShellState.ResolveCulture(
                context.CultureSelector, sessionOverride, browserLocale, context.IsUnderAdminPath);
            js.InvokeVoid("groveTheme.setAdminCulture", context.CultureSelector.CookieName, context.CultureSelector.CookiePath, resolved);
        }
        catch (JSException)
        {
        }
    }

    public void Clear() => _token = null;

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
                ? new Uri(TokenPath, UriKind.Relative)
                : new Uri(BaseAddress, TokenPath);
            using var request = new HttpRequestMessage(HttpMethod.Get, tokenUri);
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
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
        !string.Equals(request.RequestUri?.AbsolutePath, "/" + TokenPath, StringComparison.OrdinalIgnoreCase);
}

public interface IAntiforgeryTokenStore
{
    void Clear();
}

// Phase 8: AdminShellState's seam onto the browser-side culture-cookie rewrite, so it
// can depend on an interface instead of the concrete WASM-only AntiforgeryHandler
// (whose ctor hard-requires IJSInProcessRuntime and can't be constructed server-side).
// WASM: implemented by AntiforgeryHandler (rewrites the cookie via synchronous JS
// interop before each request). Server (SSR/InteractiveServer): a no-op registration -
// the server *reads* the culture cookie through RequestLocalizationOptions and never
// writes it; cookie writeback stays exclusively client-side per docs/localization.md.
public interface ICultureCookieWriter
{
    void SetCultureCookieContext(CultureCookieContext? context);
}

public sealed record GroveAntiforgeryToken(string HeaderName, string RequestToken);

// Pushed into AntiforgeryHandler by AdminShellState.RefreshManifestAsync (see
// docs/localization.md phase 15) so the handler can independently re-resolve and
// rewrite the culture cookie on every outgoing request without depending on
// AdminShellState itself (avoids a circular DI dependency - AdminShellState depends on
// IApi, which depends on this handler's owning HttpClient).
public sealed record CultureCookieContext(string? UserName, CultureSelector CultureSelector, bool IsUnderAdminPath);

/// <summary>
/// <see cref="Grove.Components.Modules.IGroveAntiforgery"/> for module clients: its own
/// cookie-carrying request to the token endpoint, cached for the session.
/// </summary>
public sealed class AntiforgeryTokenSource(Uri apiBaseAddress) : Grove.Components.Modules.IGroveAntiforgery
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Grove.Components.Modules.GroveAntiforgeryRequestToken? _token;

    public async Task<Grove.Components.Modules.GroveAntiforgeryRequestToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_token is not null)
        {
            return _token;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_token is null)
            {
                using var client = new HttpClient { BaseAddress = apiBaseAddress };
                using var request = new HttpRequestMessage(HttpMethod.Get, AntiforgeryHandler.TokenPath);
                request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
                using var response = await client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                var token = await response.Content.ReadFromJsonAsync<GroveAntiforgeryToken>(cancellationToken)
                    ?? throw new InvalidOperationException("Grove did not return an antiforgery token.");
                _token = new(token.HeaderName, token.RequestToken);
            }

            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }
}
