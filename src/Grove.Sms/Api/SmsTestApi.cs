using Grove.Api;
using Grove.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Grove.Sms.Api;

/// <summary>
/// <c>api/grove/sms</c>: the enabled SMS providers and a test send, for the SMS test page; both
/// need <c>ManageSmsSettings</c>.
/// </summary>
public sealed class SmsTestApi : ApiContributor
{
    public override string Name => "SmsTest";

    public override string FeatureId => "Grove.Sms";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("sms");
        routes.MapGet("providers", ApiHandlers.Handle<SmsTestHandlers>(h => h.ListProvidersAsync()));
        routes.MapPost("test", (HttpContext http, [FromBody] SmsTestRequest request) => ApiHandlers.For<SmsTestHandlers>(http).SendTestAsync(request));
    }
}

public sealed class SmsTestHandlers(
    IAuthorizationService authorization,
    IOptionsMonitor<SmsProviderOptions> providerOptions,
    ISmsProviderResolver providers,
    IPhoneFormatValidator phoneFormat,
    IStringLocalizer<SmsTestHandlers> S) : ApiHandlers
{
    public async Task<IResult> ListProvidersAsync()
    {
        if (!await CanManageAsync()) return Forbid();

        return Ok(providerOptions.CurrentValue.Providers
            .Where(entry => entry.Value.IsEnabled)
            .Select(entry => entry.Key)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    public async Task<IResult> SendTestAsync(SmsTestRequest request)
    {
        if (!await CanManageAsync()) return Forbid();

        var provider = await providers.GetAsync(request.Provider);
        if (provider is null)
        {
            return ApiProblems.Validation("provider", S["Please select a valid provider."]);
        }

        if (!phoneFormat.IsValid(request.PhoneNumber))
        {
            return ApiProblems.Validation("phoneNumber", S["Please provide a valid phone number."]);
        }

        var result = await provider.SendAsync(new SmsMessage
        {
            To = request.PhoneNumber,
            Body = S["This is a test SMS message."],
        }, HttpContext.RequestAborted);

        return Ok(new SmsTestResult(result.Succeeded, [.. result.Errors.Select(error => error.Message?.Value ?? error.Key)]));
    }

    private Task<bool> CanManageAsync() => authorization.AuthorizeAsync(User, SmsPermissions.ManageSmsSettings);
}
