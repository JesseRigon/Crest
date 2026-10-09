using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.Contents;
using Crest.DisplayManagement.Views;
using Crest.Modules;
using Crest.PublishLater.Models;
using Crest.PublishLater.ViewModels;

namespace Crest.PublishLater.Drivers;

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

    public override IDisplayResult Edit(PublishLaterPart part, BuildPartEditorContext context)
    {
        return Initialize<PublishLaterPartViewModel>(GetEditorShapeType(context),
            model => PopulateViewModel(part, model))
        .Location("Actions:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(PublishLaterPart part, UpdatePartEditorContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (await _authorizationService.AuthorizeAsync(httpContext?.User, CommonPermissions.PublishContent, part.ContentItem))
        {
            var viewModel = new PublishLaterPartViewModel();

            await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

            if (viewModel.ScheduledPublishLocalDateTime == null || httpContext.Request.Form["submit.Save"] == "submit.CancelPublishLater")
            {
                part.ScheduledPublishUtc = null;
            }
            else
            {
                part.ScheduledPublishUtc = await _localClock.ConvertToUtcAsync(viewModel.ScheduledPublishLocalDateTime.Value);
            }
        }

        return Edit(part, context);
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
