using Grove.Entities;

namespace Grove.Users.Models;

public class LoginForm : Entity
{
    public string UserName { get; set; }

    public string Password { get; set; }

    public bool RememberMe { get; set; }
}
