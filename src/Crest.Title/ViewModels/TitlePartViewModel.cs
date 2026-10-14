using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;
using Crest.Title.Models;

namespace Crest.Title.ViewModels;

public class TitlePartViewModel
{
    public string Title { get; set; }

    /// <summary>The item's link on the site (its permalink, else its item route), for the summary display's link.</summary>
    public string Url
    {
        get
        {
            if (ContentItem is null)
            {
                return null;
            }

            System.Text.Json.Nodes.JsonObject content = ContentItem.Content;
            return content["AutoroutePart"]?["Path"] is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var path) && path.Length > 0
                ? path.TrimStart('/')
                : "Contents/ContentItems/" + Uri.EscapeDataString(ContentItem.ContentItemId);
        }
    }

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public TitlePart TitlePart { get; set; }

    [BindNever]
    public TitlePartSettings Settings { get; set; }
}
