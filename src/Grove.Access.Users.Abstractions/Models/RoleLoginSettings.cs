namespace Grove.Access.Users.Models;

public class RoleLoginSettings
{
    public bool RequireTwoFactorAuthenticationForSpecificRoles { get; set; }

    public string[] Roles { get; set; }
}
