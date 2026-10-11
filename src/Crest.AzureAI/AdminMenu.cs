using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Crest.Indexing;
using Crest.Navigation;
using Crest.AzureAI.Drivers;
using Crest.AzureAI.Models;

namespace Crest.AzureAI;

public sealed class AdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary s_routeValues = new()
    {
        { "area", "Crest.Settings" },
        { "groupId", AzureAISearchDefaultSettingsDisplayDriver.GroupId},
    };

    private readonly IOptionsMonitor<AzureAISearchDefaultOptions> _azureAISearchSettings;

    internal readonly IStringLocalizer S;

    public AdminMenu(
        IOptionsMonitor<AzureAISearchDefaultOptions> azureAISearchSettings,
        IStringLocalizer<AdminMenu> stringLocalizer)
    {
        _azureAISearchSettings = azureAISearchSettings;
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (_azureAISearchSettings.CurrentValue.DisableUIConfiguration)
        {
            return ValueTask.CompletedTask;
        }

        if (NavigationHelper.UseLegacyFormat())
        {
            builder
               .Add(S["Configuration"], configuration => configuration
                   .Add(S["Settings"], settings => settings
                       .Add(S["Azure AI Search"], S["Azure AI Search"].PrefixPosition(), azureAISearch => azureAISearch
                       .AddClass("azure-ai-search")
                           .Id("azureaisearch")
                           .Action("Index", "Admin", s_routeValues)
                           .Permission(AzureAISearchPermissions.ManageAzureAISearchISettings)
                           .LocalNav()
                       )
                   )
               );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Search"], S["Search"].PrefixPosition(), search => search
                    .Add(S["Azure AI Search"], S["Azure AI Search"].PrefixPosition(), azureAISearch => azureAISearch
                    .AddClass("azure-ai-search")
                        .Id("azureaisearch")
                        .Action("Index", "Admin", s_routeValues)
                        .Permission(IndexingPermissions.ManageIndexes)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
