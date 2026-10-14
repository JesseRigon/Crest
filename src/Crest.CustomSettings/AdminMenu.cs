using System.Collections.Concurrent;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Crest.CustomSettings.Services;
using Crest.Mvc.Utilities;
using Crest.Admin;
using Crest.Navigation;
using Microsoft.Extensions.Options;

namespace Crest.CustomSettings;

public sealed class AdminMenu : AdminNavigationProvider
{
    private static readonly ConcurrentDictionary<string, RouteValueDictionary> _routeValues = [];

    private readonly CustomSettingsService _customSettingsService;

    internal readonly IStringLocalizer S;

    private readonly string _adminPath;

    public AdminMenu(
        IStringLocalizer<AdminMenu> localizer,
        CustomSettingsService customSettingsService,
        IOptions<AdminOptions> adminOptions)
    {
        S = localizer;
        _customSettingsService = customSettingsService;
        _adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        foreach (var type in await _customSettingsService.GetAllSettingsTypesAsync())
        {
            if (!_routeValues.TryGetValue(type.Name, out var routeValues))
            {
                routeValues = new RouteValueDictionary()
                {
                     { "area", "Crest.Settings" },
                     { "groupId", type.Name },
                };

                _routeValues[type.Name] = routeValues;
            }

            var htmlName = type.Name.HtmlClassify();

            if (NavigationHelper.UseLegacyFormat())
            {
                builder
                .Add(S["Configuration"], configuration => configuration
                    .Add(S["Settings"], settings => settings
                        .Add(new LocalizedString(type.DisplayName, type.DisplayName), type.DisplayName.PrefixPosition(), customSettings => customSettings
                            .Url($"{_adminPath}/CustomSettings/{Uri.EscapeDataString(type.Name)}")
                            .AddClass(htmlName)
                            .Id(htmlName)
                            .Permission(Permissions.CreatePermissionForType(type))
                            .Resource(type.Name)
                            .LocalNav()
                        )
                    )
                );

                continue;
            }

            builder
                .Add(S["Settings"], settings => settings
                    .Add(new LocalizedString(type.DisplayName, type.DisplayName), type.DisplayName.PrefixPosition(), layers => layers
                        .Url($"{_adminPath}/CustomSettings/{Uri.EscapeDataString(type.Name)}")
                        .AddClass(htmlName)
                        .Id(htmlName)
                        .Permission(Permissions.CreatePermissionForType(type))
                        .Resource(type.Name)
                        .LocalNav()
                    )
                );
        }
    }
}
