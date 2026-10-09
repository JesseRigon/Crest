namespace Crest.Users.Models;

public class TwoFactorOptions
{
    public IList<string> Providers { get; init; } = [];
}
