namespace Grove.Access.Users;

/// <summary>
/// The tenant's sign-in address (the <c>Grove_Users</c> shell configuration section): the staff
/// login page's path, which the shell registry serves the admin login shell at. The account pages
/// (password, email, external logins, two-factor) are fixed paths their page components declare
/// (<c>AccountPages</c>); signing out is <c>api/grove/auth/logout</c>.
/// </summary>
public class UserOptions
{
    private string _loginPath = "Login";

    public string LoginPath
    {
        get => _loginPath;
        set => _loginPath = value.Trim(' ', '/');
    }
}
