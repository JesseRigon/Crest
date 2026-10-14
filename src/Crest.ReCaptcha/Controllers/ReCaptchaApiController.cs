using Crest.ReCaptcha.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Crest.ReCaptcha.Controllers;

/// <summary>
/// What a <c>Captcha</c> component needs to render the widget (docs/widgets.md): the public site
/// key and the script. Anonymous, like the widget itself; the secret never leaves the server.
/// 404 when ReCaptcha is not configured, so the component renders nothing.
/// </summary>
[ApiController]
[Route("api/crest/recaptcha")]
public sealed class ReCaptchaApiController(IOptionsMonitor<ReCaptchaSettings> settings) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<ReCaptchaClientSettings> Get()
    {
        var current = settings.CurrentValue;
        return current.ConfigurationExists()
            ? Ok(new ReCaptchaClientSettings(current.SiteKey, current.ReCaptchaScriptUri))
            : NotFound();
    }
}

public sealed record ReCaptchaClientSettings(string SiteKey, string ScriptUri);
