using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.RateLimits.Models;

namespace Grove.RateLimits.Drivers;

public sealed class RateLimitLimiterDisplayDriver : DisplayDriver<RateLimitLimiter>
{
    public override Task<IDisplayResult> DisplayAsync(RateLimitLimiter limiter, BuildDisplayContext context)
    {
        return CombineAsync(
            View("RateLimitLimiter_Buttons_SummaryAdmin", limiter).Location(PlatformConstants.DisplayType.SummaryAdmin, "Actions:1")
        );
    }
}
