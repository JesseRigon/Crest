using System.ComponentModel.DataAnnotations;

namespace Crest.Google.TagManager.ViewModels;

public class GoogleTagManagerSettingsViewModel
{
    [Required(AllowEmptyStrings = false)]
    public string ContainerID { get; set; }
}
