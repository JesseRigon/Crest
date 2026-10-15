using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Grove.Display.Icons;
using Grove.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace Grove.Api;

/// <summary>The host's <c>api/grove/icons/tenant</c> endpoints.</summary>
public sealed class TenantIconsApi : ApiContributor
{
    public override string Name => "TenantIcons";

    public override string FeatureId => "Grove";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("icons/tenant");
        routes.MapGet("", ApiHandlers.Handle<TenantIconsHandlers>(h => h.ListAsync()));
        routes.MapPost("", (HttpContext http, [FromForm] IFormFile file, [FromForm] bool overwrite = true) => ApiHandlers.For<TenantIconsHandlers>(http).UploadAsync(file, overwrite))
            .WithMetadata(new RequestSizeLimitAttribute(TenantMediaIconProvider.MaxSvgBytes + 32 * 1024))
            .DisableAntiforgery();
        routes.MapDelete("{name}", (HttpContext http, [FromRoute] string name) => ApiHandlers.For<TenantIconsHandlers>(http).DeleteAsync(name));
    }
}

public sealed class TenantIconsHandlers(
    IAuthorizationService authorizationService,
    IEnumerable<IIconProvider> iconProviders) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        var provider = GetProvider();
        if (provider is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync())
        {
            return Forbid();
        }

        return Ok(await provider.ListAsync(HttpContext.RequestAborted));
    }

    public async Task<IResult> UploadAsync(IFormFile file, bool overwrite = true)
    {
        var provider = GetProvider();
        if (provider is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync())
        {
            return Forbid();
        }

        if (file.Length == 0 || !file.FileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("A non-empty SVG file is required.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await provider.SaveAsync(file.FileName, stream, overwrite, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    public async Task<IResult> DeleteAsync(string name)
    {
        var provider = GetProvider();
        if (provider is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync())
        {
            return Forbid();
        }

        return await provider.DeleteAsync(name, HttpContext.RequestAborted) ? NoContent() : NotFound();
    }

    private TenantMediaIconProvider? GetProvider() => iconProviders.OfType<TenantMediaIconProvider>().FirstOrDefault();

    private Task<bool> CanManageAsync() => authorizationService.AuthorizeAsync(User, IconPermissions.ManageTenantIcons);
}
