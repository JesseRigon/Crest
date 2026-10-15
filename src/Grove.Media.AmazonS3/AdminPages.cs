using Grove.Routing;

namespace Grove.Media.AmazonS3;

/// <summary>
/// The Amazon S3 media options: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Legacy("amazon-s3-options", "Amazon S3 Options", "settings-media", "Grove.Media.AmazonS3", "Admin", "Options", [Permissions.ViewAmazonS3MediaOptions.Name]);
    }
}
