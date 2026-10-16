using System.ComponentModel.DataAnnotations;

namespace Grove.Contents.Localization.Models;

public class ContentRequestCultureProviderSettings
{
    public const string GroupId = "ContentRequestCultureProvider";

    [Display(Name = "Set Cookie", Description = "Sets the localization cookie based on the Localization culture when accessing a page in the frontend.")]
    public bool SetCookie { get; set; }
}
