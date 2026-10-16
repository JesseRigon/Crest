using Grove.Contents.AdminMenu.Models;

namespace Grove.Contents.Lists.AdminNodes;

public class ListsAdminNode : AdminNode
{
    public string ContentType { get; set; }

    public bool AddContentTypeAsParent { get; set; } = true;

    public string IconForParentLink { get; set; }

    public string IconForContentItems { get; set; }
}
