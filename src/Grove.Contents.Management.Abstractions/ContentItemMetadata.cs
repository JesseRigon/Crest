using Microsoft.AspNetCore.Routing;

namespace Grove.Contents.Management;

/// <summary>
/// What a content item answers about itself: the route values of its public display (Autoroute,
/// the content route) and its display and editor groups. Its admin pages are not route values:
/// they are the native pages of the page registry (<see cref="ContentAdminPaths"/>).
/// </summary>
public class ContentItemMetadata
{
    public RouteValueDictionary DisplayRouteValues { get; set; }

    public readonly IList<GroupInfo> DisplayGroupInfo = [];
    public readonly IList<GroupInfo> EditorGroupInfo = [];
}
