using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.RateLimits.Models;

namespace Crest.RateLimits.Drivers;

public sealed class RateLimitLimiterDisplayDriver : DisplayDriver<RateLimitLimiter>
{
    public override Task<IDisplayResult> DisplayAsync(RateLimitLimiter limiter, BuildDisplayContext context)
    {
        return CombineAsync(
            View("RateLimitLimiter_Buttons_SummaryAdmin", limiter).Location(PlatformConstants.DisplayType.SummaryAdmin, "Actions:1")
        );
    }
}
