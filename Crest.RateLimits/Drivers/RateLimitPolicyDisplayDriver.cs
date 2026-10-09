using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.RateLimits.Models;

namespace Crest.RateLimits.Drivers;

public sealed class RateLimitPolicyDisplayDriver : DisplayDriver<RateLimitPolicy>
{
    public override IDisplayResult Display(RateLimitPolicy model, BuildDisplayContext context)
        => View("RateLimitPolicy_ActionsMenuItems_SummaryAdmin", model)
            .Location(PlatformConstants.DisplayType.SummaryAdmin, "ActionsMenu:5");
}
