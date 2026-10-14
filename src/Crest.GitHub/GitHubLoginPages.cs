using Crest.Routing;

namespace Crest.GitHub;

/// <summary>
/// The GitHub authentication settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class GitHubLoginPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("github", "GitHub", "settings-authentication", GitHubConstants.Features.GitHubAuthentication, [Permissions.ManageGitHubAuthentication.Name]);
    }
}
