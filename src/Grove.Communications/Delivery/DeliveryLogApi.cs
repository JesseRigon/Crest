using Grove.Api;
using Grove.Communications.Delivery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Communications.Delivery;

/// <summary>
/// <c>api/grove/communications/deliveries</c>: the delivery log, newest first, filtered by
/// channel, outcome, recipient and time; needs <c>ViewDeliveryLog</c>.
/// </summary>
public sealed class DeliveryLogApi : ApiContributor
{
    public override string Name => "DeliveryLog";

    public override string FeatureId => CommunicationsConstants.ScreensFeatureId;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("communications/deliveries");
        routes.MapGet("", (
            HttpContext http,
            [FromQuery] string channel,
            [FromQuery] DeliveryOutcome? outcome,
            [FromQuery] string recipient,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? skip,
            [FromQuery] int? take) => ApiHandlers.For<DeliveryLogHandlers>(http).ListAsync(new DeliveryLogQuery
            {
                Channel = channel,
                Outcome = outcome,
                Recipient = recipient,
                FromUtc = from,
                ToUtc = to,
                Skip = skip ?? 0,
                Take = take ?? 50,
            }));
    }
}

public sealed class DeliveryLogHandlers(IAuthorizationService authorization, IDeliveryLog log) : ApiHandlers
{
    public async Task<IResult> ListAsync(DeliveryLogQuery query)
    {
        if (!await authorization.AuthorizeAsync(User, CommunicationsPermissions.ViewDeliveryLog))
        {
            return Forbid();
        }

        return Ok(await log.ListAsync(query, HttpContext.RequestAborted));
    }
}
