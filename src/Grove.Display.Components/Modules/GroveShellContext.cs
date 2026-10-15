namespace Grove.Display.Components.Modules;

/// <summary>
/// The shell this client runs in and the organization it acts in: what every API request
/// declares in its <c>X-Shell</c> and <c>X-Org</c> headers so the server builds the caller
/// for that side and never defaults one. Set once at boot (the shell) and by the member
/// shell when the organization changes.
/// </summary>
public sealed class GroveShellContext
{
    public const string Admin = "admin";

    public const string Member = "member";

    public const string Site = "site";

    public string Shell { get; set; } = Site;

    /// <summary>The tenant's browser-absolute base path ("" or "/t2"), as the server sent it.</summary>
    public string TenantBase { get; set; } = string.Empty;

    public string? OrganizationId { get; set; }

    /// <summary>
    /// The organization slug the member document's path names ("/members/{slug}/"), or null
    /// for a document outside any organization. The member shell resolves it to
    /// <see cref="OrganizationId"/> from the member's own organizations.
    /// </summary>
    public string? OrganizationSlug { get; set; }
}
