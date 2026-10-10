namespace Crest.Users.Models;

public class TwoFactorMethod
{
    public string Provider { get; set; }

    public bool IsEnabled { get; set; }
}
