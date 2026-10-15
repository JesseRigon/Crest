using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.RateLimits.Models;

namespace Grove.RateLimits.Drivers;

public sealed class RateLimitPolicyDisplayDriver : DisplayDriver<RateLimitPolicy>
{
    public override IDisplayResult Display(RateLimitPolicy model, BuildDisplayContext context)
        => View("RateLimitPolicy_ActionsMenuItems_SummaryAdmin", model)
            .Location(PlatformConstants.DisplayType.SummaryAdmin, "ActionsMenu:5");
}
