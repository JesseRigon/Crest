#nullable enable

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;

namespace Grove.Access.Api;

public sealed class AntiforgeryApi : ApiContributor
{
    public override string Name => "Antiforgery";

    public override string FeatureId => "Grove.Access";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("antiforgery");
        routes.MapGet("token", ApiHandlers.Handle<AntiforgeryHandlers>(h => h.GetToken()));
    }
}


/// <summary>
/// Supplies Grove's normal antiforgery request token to the same-origin WASM
/// client. The token remains bound to the browser's Grove cookie and user.
/// </summary>
public sealed class AntiforgeryHandlers(IAntiforgery antiforgery) : ApiHandlers
{
    public IResult GetToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryToken(tokens.HeaderName ?? "RequestVerificationToken", tokens.RequestToken ?? string.Empty));
    }
}

public sealed record AntiforgeryToken(string HeaderName, string RequestToken);
