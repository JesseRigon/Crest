using System.ComponentModel.DataAnnotations;

namespace Grove.ContentLocalization.Models;

public class ContentCulturePickerSettings
{
    public const string GroupId = "ContentCulturePicker";

    [Display(Name = "Homepage redirect", Description = "Redirects to the homepage of the target culture if the ContentItem of the target culture does not exist.", Order = 1)]
    public bool RedirectToHomepage { get; set; }

    [Display(Name = "Set Cookie", Description = "Sets the localization cookie when switching cultures using the ContentCulturePicker", Order = 0)]
    public bool SetCookie { get; set; } = true;
}
