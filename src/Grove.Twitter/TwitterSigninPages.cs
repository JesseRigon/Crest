using Grove.Routing;

namespace Grove.Twitter;

/// <summary>
/// The sign-in with X (Twitter) settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class TwitterSigninPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("twitter-signin", "Sign In with X (Twitter)", "settings-authentication", TwitterConstants.Features.Signin, [Permissions.ManageTwitterSignin.Name]);
    }
}
