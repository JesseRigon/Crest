using Grove.ContentManagement.Display;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement;
using Grove.DisplayManagement.ModelBinding;
using Grove.DisplayManagement.Views;
using Grove.Lists.Models;
using Grove.Lists.Services;
using Grove.Lists.ViewModels;
using Grove.Navigation;
using ISession = YesSql.ISession;

namespace Grove.Lists.Drivers;

public sealed class ListPartDisplayDriver : ContentPartDisplayDriver<ListPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContainerService _containerService;
    private readonly IShapeFactory _shapeFactory;
    private readonly PagerOptions _pagerOptions;
    private readonly IServiceProvider _serviceProvider;

    public ListPartDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        IContainerService containerService,
        IShapeFactory shapeFactory,
        IOptions<PagerOptions> pagerOptions,
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _contentDefinitionManager = contentDefinitionManager;
        _containerService = containerService;
        _shapeFactory = shapeFactory;
        _pagerOptions = pagerOptions.Value;
    }

    public override IDisplayResult Display(ListPart listPart, BuildPartDisplayContext context)
    {
        // The admin shapes (header, navigation, search panel, summary) are gone with the MVC
        // list screens: the native editor's ListPartEditor lists the contained items.
        return InitializeDisplayListPartDisplayShape(listPart, context);
    }

    private ShapeResult InitializeDisplayListPartDisplayShape(ListPart listPart, BuildPartDisplayContext context)
    {
        return Initialize<ListPartViewModel>(GetDisplayShapeType(context), async model =>
        {
            var settings = context.TypePartDefinition.GetSettings<ListPartSettings>();
            var containedItemOptions = new ContainedItemOptions();

            model.ContainedContentTypeDefinitions = await GetContainedContentTypesAsync(settings);
            model.Context = context;
            model.ListPart = listPart;

            if (settings.ShowFullPager)
            {
                var pager = await GetPagerAsync(context);

                model.ContentItems = await _containerService.QueryContainedItemsAsync(
                    listPart.ContentItem.ContentItemId,
                    settings.EnableOrdering,
                    pager,
                    containedItemOptions);

                containedItemOptions.Status = ContentsStatus.Published;
                var totalItemCount = await _containerService.GetItemCountAsync(listPart.ContentItem.ContentItemId, containedItemOptions);

                model.Pager = await _shapeFactory.PagerAsync(pager, totalItemCount);
            }
            else
            {
                var pagerSlim = await GetPagerSlimAsync(context);

                model.ContentItems = await _containerService.QueryContainedItemsAsync(
                    listPart.ContentItem.ContentItemId,
                    settings.EnableOrdering,
                    pagerSlim,
                    containedItemOptions);

                model.Pager = await _shapeFactory.PagerSlimAsync(pagerSlim);
            }
        })
        // The contained items' summaries as the shape's items (docs/blazor-display.md rework
        // step 7): the display tree maps them as the ListPart component's children. The Razor
        // view builds its own and ignores them.
        .Processing(async shape =>
        {
            if (shape is not ListPartViewModel { ContentItems: { } items } list)
            {
                return;
            }

            var displayManager = _serviceProvider.GetRequiredService<IContentItemDisplayManager>();
            foreach (var item in items)
            {
                await shape.AddAsync(await displayManager.BuildDisplayAsync(item, context.Updater, PlatformConstants.DisplayType.Summary, context.GroupId));
            }

            // The pager after the items, mapped to its own component.
            if (list.Pager is IShape pager)
            {
                await shape.AddAsync(pager);
            }
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content:10");
    }

    private async Task<PagerSlim> GetPagerSlimAsync(BuildPartDisplayContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<ListPartSettings>();
        var pagerParameters = new PagerSlimParameters();
        await context.Updater.TryUpdateModelAsync(pagerParameters);

        var pageSize = _pagerOptions.GetPageSize(pagerParameters.PageSize, settings.PageSize);

        return new PagerSlim(pagerParameters.Before, pagerParameters.After, pageSize);
    }

    private async Task<Pager> GetPagerAsync(BuildPartDisplayContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<ListPartSettings>();
        var pagerParameters = new PagerParameters();
        await context.Updater.TryUpdateModelAsync(pagerParameters);

        var pageSize = _pagerOptions.GetPageSize(pagerParameters.PageSize, settings.PageSize);

        return new Pager(pagerParameters.Page, pageSize, pageSize);
    }

    private async Task<IEnumerable<ContentTypeDefinition>> GetContainedContentTypesAsync(ListPartSettings settings)
    {
        var contentTypes = settings.ContainedContentTypes ?? [];
        var definitions = new List<ContentTypeDefinition>();

        foreach (var contentType in contentTypes)
        {
            var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

            if (definition == null)
            {
                continue;
            }

            definitions.Add(definition);
        }

        return definitions;
    }
}
