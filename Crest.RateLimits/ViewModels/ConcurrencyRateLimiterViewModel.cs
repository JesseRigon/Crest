using Microsoft.AspNetCore.Mvc.Rendering;
using System.Threading.RateLimiting;

namespace Crest.RateLimits.ViewModels;

public class ConcurrencyRateLimiterViewModel
{
    public int PermitLimit { get; set; }

    public int QueueLimit { get; set; }

    public QueueProcessingOrder QueueProcessingOrder { get; set; }

    public IEnumerable<SelectListItem> QueueProcessingOrders { get; set; } = [];
}
