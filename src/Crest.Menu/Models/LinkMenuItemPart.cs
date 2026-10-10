using Crest.ContentManagement;

namespace Crest.Menu.Models;

public class LinkMenuItemPart : ContentPart
{
    /// <summary>
    /// The url of the link to create.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// The target of the link to create.
    /// </summary>
    public string Target { get; set; }
}
