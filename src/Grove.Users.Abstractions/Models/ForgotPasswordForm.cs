using Grove.Entities;

namespace Grove.Users.Models;

public class ForgotPasswordForm : Entity
{
    public string UsernameOrEmail { get; set; }
}
