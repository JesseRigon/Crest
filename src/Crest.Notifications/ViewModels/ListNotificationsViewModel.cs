using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Notifications.Models;

namespace Crest.Notifications.ViewModels;

public class ListNotificationsViewModel
{
    public ListNotificationOptions Options { get; set; }

    [BindNever]
    public IEnumerable<dynamic> Notifications { get; set; }

    [BindNever]
    public dynamic Header { get; set; }

    [BindNever]
    public dynamic Pager { get; set; }
}
