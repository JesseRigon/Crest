using Grove.Api;
using Grove.Security.ReCaptcha.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Grove.Security.ReCaptcha.Api;

/// <summary>
/// <c>api/grove/recaptcha</c>: what a <c>Captcha</c> component needs to render the widget
/// (docs/widgets.md): the public site key and the script. Anonymous, like the widget itself; the
/// secret never leaves the server. 404 when ReCaptcha is not configured, so the component renders
/// nothing.
/// </summary>
public sealed class ReCaptchaApi : ApiContributor
{
    public override string Name => "ReCaptcha";

    public override string FeatureId => "Grove.Security.ReCaptcha";

    public override void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("recaptcha", ApiHandlers.Handle<ReCaptchaHandlers>(h => h.Get())).AllowAnonymous();
    }
}

public sealed class ReCaptchaHandlers(IOptionsMonitor<ReCaptchaSettings> settings) : ApiHandlers
{
    public IResult Get()
    {
        var current = settings.CurrentValue;
        return current.ConfigurationExists()
            ? Ok(new ReCaptchaClientSettings(current.SiteKey, current.ReCaptchaScriptUri))
            : NotFound();
    }
}

public sealed record ReCaptchaClientSettings(string SiteKey, string ScriptUri);
