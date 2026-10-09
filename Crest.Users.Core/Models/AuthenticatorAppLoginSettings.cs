namespace Crest.Users.Models;

public class AuthenticatorAppLoginSettings
{
    public bool UseEmailAsAuthenticatorDisplayName { get; set; }

    public int TokenLength { get; set; } = 6;
}
