using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Crest.MemberTheme.Services;
using Crest.Members.Models;
using Microsoft.AspNetCore.Components;

namespace Crest.Members.Member.Services;

/// <summary>
/// Supplies the member shell's chrome from the Members module's own portal APIs.
/// </summary>
/// <remarks>
/// This is the downstream half of Crest's member-shell seam: Crest owns the shell and
/// declares <see cref="IMemberShellContext"/>; this module owns what a member IS - the
/// session, the organization bindings, the active organization - and injects itself upward.
/// Crest never references this module.
///
/// <para>
/// Everything here is read from the server for the CURRENT member. The shell is one theme
/// shared by every organization-bound user in the tenant, so none of this is an
/// authorization decision: the server decides which bindings this principal has, and this
/// class only renders them. Switching the active organization is likewise a server
/// decision - the POST either succeeds for a binding this member holds or it does not.
/// </para>
/// </remarks>
public sealed class MemberShellContext(HttpClient http, Crest.Components.Modules.CrestShellContext shell) : IMemberShellContext
{
    private MemberSessionModel? _session;
    private bool _loaded;

    public string SiteName { get; private set; } = "Members";

    public string UserName => _session?.Member?.UserName ?? string.Empty;

    public IReadOnlyList<MemberShellOrganization> Organizations { get; private set; } = [];

    public string? ActiveOrganizationId => _session?.ActiveOrganizationId;

    public string? ActiveOrganizationName =>
        Organizations.FirstOrDefault(organization => organization.Id == ActiveOrganizationId)?.Name;

    // This module contributes no member pages of its own - the shell already links the
    // generic ones (account, sign out) - so it adds no nav entries.
    public IReadOnlyList<MemberShellNavigationEntry> NavigationEntries { get; } = [];

    public string AccountPath => Crest.MemberTheme.MemberRoutePaths.Account;

    public string SignOutText => "Sign out";

    public event Action? Changed;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            using var response = await http.GetAsync("api/crest/members/me");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Not signed in. The middleware redirects member pages to the member
                // login, so this is the anonymous [AllowAnonymous] case: render empty
                // chrome rather than throwing.
                _session = null;
                Organizations = [];
                return;
            }

            response.EnsureSuccessStatusCode();
            _session = await response.Content.ReadFromJsonAsync<MemberSessionModel>();
            // Every later request names this organization (X-Org); the server validates it
            // against the member's bindings on each one.
            shell.OrganizationId = _session?.ActiveOrganizationId;

            // Names come with the session, resolved by the server: an organization the
            // member is bound to is not thereby a content item they may read.
            Organizations =
            [
                .. (_session?.Organizations ?? [])
                    .Select(organization => new MemberShellOrganization(organization.Id, organization.Name))
            ];
        }
        catch (HttpRequestException)
        {
            // A shell that cannot reach its own API renders empty rather than breaking
            // the document: the pages inside it report their own failures.
            _session = null;
            Organizations = [];
        }
        finally
        {
            Changed?.Invoke();
        }
    }

    public async Task SetActiveOrganizationAsync(string organizationId)
    {
        using var response = await http.PostAsJsonAsync("api/crest/members/active-org", new { organizationId });
        if (!response.IsSuccessStatusCode)
        {
            // The server refused: this member does not hold that binding. Leave the
            // active organization as it was.
            return;
        }

        await LoadAsync();
    }

    public async Task SignOutAsync()
    {
        using var response = await http.PostAsync("api/crest/auth/logout", null);
        _session = null;
        Organizations = [];
        Changed?.Invoke();
    }
}
