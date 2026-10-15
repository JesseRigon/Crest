using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Settings;
using Grove.ContentFields.ViewModels;
using Grove.ContentLocalization;
using Grove.ContentManagement;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.Contents;
using Grove.DisplayManagement.Views;
using Grove.Liquid;
using Grove.Localization;
using Grove.Mvc.ModelBinding;

namespace Grove.ContentFields.Drivers;

public sealed class ContentPickerFieldDisplayDriver : ContentFieldDisplayDriver<ContentPickerField>
{
    private readonly IContentManager _contentManager;
    private readonly ILiquidTemplateManager _templateManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public ContentPickerFieldDisplayDriver(
        IContentManager contentManager,
        IStringLocalizer<ContentPickerFieldDisplayDriver> localizer,
        ILiquidTemplateManager templateManager,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _contentManager = contentManager;
        S = localizer;
        _templateManager = templateManager;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Display(ContentPickerField field, BuildFieldDisplayContext fieldDisplayContext)
    {
        return Initialize<DisplayContentPickerFieldViewModel>(GetDisplayShapeType(fieldDisplayContext), async model =>
        {
            model.Field = field;
            model.Part = fieldDisplayContext.ContentPart;
            model.PartFieldDefinition = fieldDisplayContext.PartFieldDefinition;
            model.Items = (await _contentManager.GetAsync(field.ContentItemIds ?? []))
                .Select(item => new DisplayLinkItem(item.DisplayText ?? item.ContentItemId, ContentItemLinks.SitePath(item)))
                .ToArray();
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
