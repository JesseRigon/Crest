using Crest.Entities;

namespace Crest.Users.Models;

public class LoginForm : Entity
{
    public string UserName { get; set; }

    public string Password { get; set; }

    public bool RememberMe { get; set; }
}
