using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Grove.Contents.ArchiveLater.Models;
using Grove.Contents.ArchiveLater.ViewModels;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents;
using Grove.Display.Management.Views;
using Grove.Modules;
using Grove.Contents.Management;

namespace Grove.Contents.ArchiveLater.Drivers;

public sealed class ArchiveLaterFieldSetDisplayDriver : ContentFieldSetDisplayDriver<ArchiveLaterFieldSet>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILocalClock _localClock;

    public ArchiveLaterFieldSetDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ILocalClock localClock)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _localClock = localClock;
    }

    public override IDisplayResult Display(ArchiveLaterFieldSet part, BuildFieldSetDisplayContext context)
        => Initialize<ArchiveLaterFieldSetViewModel, ArchiveLaterFieldSetDisplayDriver, ArchiveLaterFieldSet>(
            $"{ContentFieldSetName.Of<ArchiveLaterFieldSet>()}_SummaryAdmin",
            static (model, driver, part) => driver.PopulateViewModel(part, model), this, part).Location(PlatformConstants.DisplayType.SummaryAdmin, "Meta:25");

    private async ValueTask PopulateViewModel(ArchiveLaterFieldSet part, ArchiveLaterFieldSetViewModel viewModel)
    {
        viewModel.ContentItem = part.ContentItem;
        viewModel.ScheduledArchiveUtc = part.ScheduledArchiveUtc;
        viewModel.ScheduledArchiveLocalDateTime = part.ScheduledArchiveUtc.HasValue
            ? (await _localClock.ConvertToLocalAsync(part.ScheduledArchiveUtc.Value)).DateTime
            : null;
    }
}
