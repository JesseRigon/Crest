using System.ComponentModel.DataAnnotations;

namespace Crest.Notifications.ViewModels;

public class ReadNotificationViewModel
{
    [Required]
    public string MessageId { get; set; }
}
