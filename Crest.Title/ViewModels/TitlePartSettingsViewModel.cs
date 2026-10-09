using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Title.Models;

namespace Crest.Title.ViewModels;

public class TitlePartSettingsViewModel
{
    public TitlePartOptions Options { get; set; }
    public string Pattern { get; set; }
    public bool RenderTitle { get; set; }

    [BindNever]
    public TitlePartSettings TitlePartSettings { get; set; }

    public string Placeholder { get; set; }
}
