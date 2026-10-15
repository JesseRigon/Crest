using System.ComponentModel.DataAnnotations;

namespace Grove.ContentLocalization.Models;

public class ContentRequestCultureProviderSettings
{
    public const string GroupId = "ContentRequestCultureProvider";

    [Display(Name = "Set Cookie", Description = "Sets the localization cookie based on the LocalizationPart culture when accessing a page in the frontend.")]
    public bool SetCookie { get; set; }
}
