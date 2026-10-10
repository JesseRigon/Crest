using Crest.Entities;

namespace Crest.Users.Models;

public class ForgotPasswordForm : Entity
{
    public string UsernameOrEmail { get; set; }
}
