using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;
using Grove.Admin;
using Grove.Facebook.Settings;
using Grove.ResourceManagement;
using Grove.Settings;

namespace Grove.Facebook.Filters;

public sealed class FBInitFilter : IAsyncResultFilter
{
    private readonly IResourceManager _resourceManager;
    private readonly ISiteService _siteService;

    public FBInitFilter(
        IResourceManager resourceManager,
        ISiteService siteService)
    {
        _resourceManager = resourceManager;
        _siteService = siteService;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Should only run on the front-end for a full view
        if (context.IsViewOrPageResult() && !AdminAttribute.IsApplied(context.HttpContext))
        {
            var site = await _siteService.GetSiteSettingsAsync();

            if (site.TryGet<FacebookSettings>(out var settings) &&
                !string.IsNullOrWhiteSpace(settings.AppId))
            {
                if (settings.FBInit)
                {
                    var setting = _resourceManager.RegisterResource("script", "fb");
                    setting.Culture = CultureInfo.CurrentUICulture.Name;
                    setting.AtLocation(ResourceLocation.Foot);
                }
            }
        }
        await next.Invoke();
    }
}
