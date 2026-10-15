using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents;
using Grove.Display.Management.Views;
using Grove.Modules;
using Grove.Contents.PublishLater.Models;
using Grove.Contents.PublishLater.ViewModels;

namespace Grove.Contents.PublishLater.Drivers;

public sealed class PublishLaterPartDisplayDriver : ContentPartDisplayDriver<PublishLaterPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILocalClock _localClock;

    public PublishLaterPartDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ILocalClock localClock)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _localClock = localClock;
    }

    public override IDisplayResult Display(PublishLaterPart part, BuildPartDisplayContext context)
    {
        return Initialize<PublishLaterPartViewModel>($"{nameof(PublishLaterPart)}_SummaryAdmin",
            model => PopulateViewModel(part, model))
            .Location(PlatformConstants.DisplayType.SummaryAdmin, "Meta:25");
    }

    private async ValueTask PopulateViewModel(PublishLaterPart part, PublishLaterPartViewModel viewModel)
    {
        viewModel.ContentItem = part.ContentItem;
        viewModel.ScheduledPublishUtc = part.ScheduledPublishUtc;
        viewModel.ScheduledPublishLocalDateTime = part.ScheduledPublishUtc.HasValue ?
            (await _localClock.ConvertToLocalAsync(part.ScheduledPublishUtc.Value)).DateTime :
            null;
    }
}
