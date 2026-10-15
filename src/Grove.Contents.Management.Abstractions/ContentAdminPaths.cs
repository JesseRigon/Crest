namespace Grove.Contents.Management;

/// <summary>
/// The native admin pages of content items, as paths under the admin shell's root (a caller
/// outside the admin shell prefixes them with the admin URL prefix): the page registry's routes of
/// <c>Pages/ContentItems.razor</c>, <c>CreateContentItem.razor</c>, <c>EditContentItem.razor</c>
/// and <c>DisplayContentItem.razor</c>.
/// </summary>
public static class ContentAdminPaths
{
    /// <summary>The content list, filtered to one type when given.</summary>
    public static string List(string contentType = null) =>
        string.IsNullOrEmpty(contentType) ? "Contents/ContentItems" : $"Contents/ContentItems?type={Uri.EscapeDataString(contentType)}";

    /// <summary>The editor of a new item of <paramref name="contentType"/>; a contained item names its list.</summary>
    public static string Create(string contentType, string containerId = null) =>
        $"Contents/ContentTypes/{Uri.EscapeDataString(contentType)}/Create"
        + (string.IsNullOrEmpty(containerId) ? string.Empty : $"?containerId={Uri.EscapeDataString(containerId)}");

    public static string Edit(string contentItemId) => $"Contents/ContentItems/{Uri.EscapeDataString(contentItemId)}/Edit";

    public static string Display(string contentItemId) => $"Contents/ContentItems/{Uri.EscapeDataString(contentItemId)}/Display";

    /// <summary>A path under the admin root as a URL path: <c>/{prefix}/{path}</c>.</summary>
    public static string InAdmin(string adminUrlPrefix, string path) => $"/{adminUrlPrefix.Trim('/')}/{path}";
}
