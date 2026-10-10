using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Alias.Models;
using Crest.Alias.Settings;
using Crest.ContentManagement;

namespace Crest.Alias.ViewModels;

public class AliasPartViewModel
{
    public string Alias { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public AliasPart AliasPart { get; set; }

    [BindNever]
    public AliasPartSettings Settings { get; set; }
}
