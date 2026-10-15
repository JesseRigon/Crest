using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Grove.ContentManagement;
using Grove.ContentManagement.Display;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Models;
using Grove.Contents;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Notify;
using Grove.DisplayManagement.Views;
using Grove.Flows.Models;
using Grove.Flows.ViewModels;
using Grove.Modules;
using Grove.Security.Permissions;

namespace Grove.Flows.Drivers;

/// <summary>
/// The bag's display: its contained items' displays as the shape's items (the editor is the
/// editor tree's <c>BagPartEditor</c>).
/// </summary>
public sealed class BagPartDisplayDriver : ContentPartDisplayDriver<BagPart>
{
    private readonly IServiceProvider _serviceProvider;

    public BagPartDisplayDriver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override IDisplayResult Display(BagPart bagPart, BuildPartDisplayContext context)
    {
        var hasItems = bagPart.ContentItems.Count > 0;

        var settings = context.TypePartDefinition.GetSettings<BagPartSettings>();
        return Initialize<BagPartViewModel>(hasItems ? "BagPart" : "BagPart_Empty", m =>
        {
            m.BagPart = bagPart;
            m.BuildPartDisplayContext = context;
            m.Settings = settings;
        })
        // The contained items' displays as the shape's items, built while the shape is processed:
        // the display tree maps them as the BagPart component's children (rework step 7). The
        // Razor view builds its own and ignores them.
        .Processing(async shape =>
        {
            if (!hasItems || shape is not IShape bag)
            {
                return;
            }

            var displayManager = _serviceProvider.GetRequiredService<IContentItemDisplayManager>();
            foreach (var item in bagPart.ContentItems)
            {
                await bag.AddAsync(await displayManager.BuildDisplayAsync(item, context.Updater, settings.DisplayType ?? "Summary", context.GroupId));
            }
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
