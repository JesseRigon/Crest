using System.ComponentModel.DataAnnotations;

namespace Grove.Google.TagManager.ViewModels;

public class GoogleTagManagerSettingsViewModel
{
    [Required(AllowEmptyStrings = false)]
    public string ContainerID { get; set; }
}
