using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Routes.Auto.Models;
using Grove.Contents.Management;

namespace Grove.Routes.Auto.ViewModels;

public class AutorouteFieldSetViewModel
{
    public string Path { get; set; }
    public bool SetHomepage { get; set; }
    public bool UpdatePath { get; set; }
    public bool IsHomepage { get; set; }
    public bool RouteContainedItems { get; set; }
    public bool Absolute { get; set; }
    public bool Disabled { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public AutorouteFieldSet AutorouteFieldSet { get; set; }

    [BindNever]
    public AutorouteFieldSetSettings Settings { get; set; }
}
