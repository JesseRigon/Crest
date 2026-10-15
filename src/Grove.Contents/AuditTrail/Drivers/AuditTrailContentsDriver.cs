using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Grove.AuditTrail;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.ViewModels;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;

namespace Grove.Contents.AuditTrail.Drivers;

public sealed class AuditTrailContentsDriver : ContentDisplayDriver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public AuditTrailContentsDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        return Task.FromResult<IDisplayResult>(
            Initialize<ContentItemViewModel>("AuditTrailContentsAction_SummaryAdmin", m => m.ContentItem = contentItem)
            .Location(PlatformConstants.DisplayType.SummaryAdmin, "ActionsMenu:10")
            .RenderWhen(static (driver) => driver._authorizationService.AuthorizeAsync(driver._httpContextAccessor.HttpContext?.User, AuditTrailPermissions.ViewAuditTrail), this)
        );
    }
}
