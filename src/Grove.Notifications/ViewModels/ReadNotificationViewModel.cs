using System.ComponentModel.DataAnnotations;

namespace Grove.Notifications.ViewModels;

public class ReadNotificationViewModel
{
    [Required]
    public string MessageId { get; set; }
}
