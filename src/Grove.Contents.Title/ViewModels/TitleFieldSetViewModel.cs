using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Management;
using Grove.Contents.Title.Models;

namespace Grove.Contents.Title.ViewModels;

public class TitleFieldSetViewModel
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
            return content["Autoroute"]?["Path"]?["Text"] is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var path) && path.Length > 0
                ? path.TrimStart('/')
                : "Contents/ContentItems/" + Uri.EscapeDataString(ContentItem.ContentItemId);
        }
    }

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public TitleFieldSet TitleFieldSet { get; set; }

    [BindNever]
    public TitleFieldSetSettings Settings { get; set; }
}
