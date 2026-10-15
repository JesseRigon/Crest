using System.ComponentModel;

namespace Grove.Access.Users.Models;

public class LoginSettings
{

    public bool DisableLocalLogin { get; set; }

    public bool AllowChangingUsername { get; set; }

    public bool AllowChangingEmail { get; set; }

    public bool AllowRememberMe { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating the default for whether the authentication cookie
    /// should be persistent across browser sessions.
    /// </summary>
    public bool UsePersistentAuthenticationCookie { get; set; }

    [DefaultValue(true)]
    public bool AllowChangingPhoneNumber { get; set; } = true;
}
