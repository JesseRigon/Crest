using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Grove.ArchiveLater.Models;
using Grove.ArchiveLater.ViewModels;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.Contents;
using Grove.DisplayManagement.Views;
using Grove.Modules;

namespace Grove.ArchiveLater.Drivers;

public sealed class ArchiveLaterPartDisplayDriver : ContentPartDisplayDriver<ArchiveLaterPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILocalClock _localClock;

    public ArchiveLaterPartDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ILocalClock localClock)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _localClock = localClock;
    }

    public override IDisplayResult Display(ArchiveLaterPart part, BuildPartDisplayContext context)
        => Initialize<ArchiveLaterPartViewModel, ArchiveLaterPartDisplayDriver, ArchiveLaterPart>(
            $"{nameof(ArchiveLaterPart)}_SummaryAdmin",
            static (model, driver, part) => driver.PopulateViewModel(part, model), this, part).Location(PlatformConstants.DisplayType.SummaryAdmin, "Meta:25");

    private async ValueTask PopulateViewModel(ArchiveLaterPart part, ArchiveLaterPartViewModel viewModel)
    {
        viewModel.ContentItem = part.ContentItem;
        viewModel.ScheduledArchiveUtc = part.ScheduledArchiveUtc;
        viewModel.ScheduledArchiveLocalDateTime = part.ScheduledArchiveUtc.HasValue
            ? (await _localClock.ConvertToLocalAsync(part.ScheduledArchiveUtc.Value)).DateTime
            : null;
    }
}
