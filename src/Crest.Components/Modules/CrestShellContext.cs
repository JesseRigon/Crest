namespace Crest.Components.Modules;

/// <summary>
/// The shell this client runs in and the organization it acts in: what every API request
/// declares in its <c>X-Shell</c> and <c>X-Org</c> headers so the server builds the caller
/// for that side and never defaults one. Set once at boot (the shell) and by the member
/// shell when the organization changes.
/// </summary>
public sealed class CrestShellContext
{
    public const string Admin = "admin";

    public const string Member = "member";

    public const string Site = "site";

    public string Shell { get; set; } = Site;

    public string? OrganizationId { get; set; }
}
