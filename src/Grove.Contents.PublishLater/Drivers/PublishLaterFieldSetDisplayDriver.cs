using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents;
using Grove.Display.Management.Views;
using Grove.Modules;
using Grove.Contents.PublishLater.Models;
using Grove.Contents.PublishLater.ViewModels;
using Grove.Contents.Management;

namespace Grove.Contents.PublishLater.Drivers;

public sealed class PublishLaterFieldSetDisplayDriver : ContentFieldSetDisplayDriver<PublishLaterFieldSet>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILocalClock _localClock;

    public PublishLaterFieldSetDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ILocalClock localClock)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _localClock = localClock;
    }

    public override IDisplayResult Display(PublishLaterFieldSet part, BuildFieldSetDisplayContext context)
    {
        return Initialize<PublishLaterFieldSetViewModel>($"{ContentFieldSetName.Of<PublishLaterFieldSet>()}_SummaryAdmin",
            model => PopulateViewModel(part, model))
            .Location(PlatformConstants.DisplayType.SummaryAdmin, "Meta:25");
    }

    private async ValueTask PopulateViewModel(PublishLaterFieldSet part, PublishLaterFieldSetViewModel viewModel)
    {
        viewModel.ContentItem = part.ContentItem;
        viewModel.ScheduledPublishUtc = part.ScheduledPublishUtc;
        viewModel.ScheduledPublishLocalDateTime = part.ScheduledPublishUtc.HasValue ?
            (await _localClock.ConvertToLocalAsync(part.ScheduledPublishUtc.Value)).DateTime :
            null;
    }
}
