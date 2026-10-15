using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Alias.Models;
using Grove.Alias.Settings;
using Grove.ContentManagement;

namespace Grove.Alias.ViewModels;

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
