using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement;
using Crest.Title.Models;

namespace Crest.Title.ViewModels;

public class TitlePartViewModel
{
    public string Title { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public TitlePart TitlePart { get; set; }

    [BindNever]
    public TitlePartSettings Settings { get; set; }
}
