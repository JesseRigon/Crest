namespace Grove.Display.Components.Primitives;

/// <summary>An organization in the shell switcher: what it is called and the URL that opens the
/// member portal with it active (<c>{tenant}/{member prefix}/{slug}/</c>).</summary>
public sealed record GroveShellSwitcherOrganization(string Id, string Name, string Url);

/// <summary>
/// One account this device has signed in to: a tenant and the user signed in there. Kept in the
/// browser only (<c>groveComponents.deviceAccounts</c>), never on a server.
/// </summary>
public sealed class GroveDeviceAccount
{
    public string TenantName { get; set; } = string.Empty;

    /// <summary>The tenant's browser-absolute base path; with the user name, the account's key.</summary>
    public string TenantBase { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    /// <summary>Where the account opens: the shell it was last used in.</summary>
    public string HomeUrl { get; set; } = string.Empty;

    public string LastSeenUtc { get; set; } = string.Empty;
}
