using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Contents.Alias.Models;
using Grove.Contents.Alias.Settings;
using Grove.Contents.Management;

namespace Grove.Contents.Alias.ViewModels;

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
