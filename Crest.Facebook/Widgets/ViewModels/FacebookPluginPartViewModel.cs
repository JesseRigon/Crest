using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;
using Crest.Facebook.Widgets.Models;
using Crest.Facebook.Widgets.Settings;

namespace Crest.Facebook.Widgets.ViewModels;

public class FacebookPluginPartViewModel
{
    public string Liquid { get; set; }
    public string Html { get; set; }

    [BindNever]
    public FacebookPluginPartSettings Settings { get; set; }

    [BindNever]
    public FacebookPluginPart FacebookPluginPart { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }
}
