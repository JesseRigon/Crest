namespace Grove.Access.Members.Models;

/// <summary>
/// The Members tenant settings (site settings). <see cref="AllowMembersOnTenantOrganization"/>
/// decides whether member bindings may point at the Organization that represents the
/// tenant; default blocked, so staff and member logins never share an organization
/// (rulings 2026-10-05, 2026-10-06).
/// </summary>
public class MembersSettings
{
    public bool AllowMembersOnTenantOrganization { get; set; }
}
