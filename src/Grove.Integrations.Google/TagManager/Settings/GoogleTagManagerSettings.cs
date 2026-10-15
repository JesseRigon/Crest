using System.ComponentModel.DataAnnotations;

namespace Grove.Integrations.Google.TagManager.Settings;

public class GoogleTagManagerSettings
{
    [Display(Name = "Container ID", Description = "The container ID defined in your Google Tag Manager account.")]
    [Required(AllowEmptyStrings = false)]
    public string ContainerID { get; set; }
}
