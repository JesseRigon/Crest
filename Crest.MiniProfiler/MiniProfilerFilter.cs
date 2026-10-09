using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Crest.Admin;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Layout;
using Crest.DisplayManagement.Shapes;

namespace Crest.MiniProfiler;

public sealed class MiniProfilerFilter : IAsyncResultFilter
{
    private readonly ILayoutAccessor _layoutAccessor;
    private readonly IShapeFactory _shapeFactory;
    private readonly IAuthorizationService _authorizationService;

    public MiniProfilerFilter(
        ILayoutAccessor layoutAccessor,
        IShapeFactory shapeFactory,
        IAuthorizationService authorizationService)
    {
        _layoutAccessor = layoutAccessor;
        _shapeFactory = shapeFactory;
        _authorizationService = authorizationService;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var viewMiniProfilerOnFrontEnd = await _authorizationService.AuthorizeAsync(context.HttpContext.User, Permissions.ViewMiniProfilerOnFrontEnd);
        var viewMiniProfilerOnBackEnd = await _authorizationService.AuthorizeAsync(context.HttpContext.User, Permissions.ViewMiniProfilerOnBackEnd);
        if (
                context.IsViewOrPageResult() &&
                (
                    (viewMiniProfilerOnFrontEnd && !AdminAttribute.IsApplied(context.HttpContext)) ||
                    (viewMiniProfilerOnBackEnd && AdminAttribute.IsApplied(context.HttpContext))
                )
            )
        {
            var layout = await _layoutAccessor.GetLayoutAsync();
            var footerZone = layout.Zones["Footer"];

            if (footerZone is Shape shape)
            {
                await shape.AddAsync(await _shapeFactory.CreateAsync("MiniProfiler"));
            }
        }

        await next.Invoke();
    }
}
