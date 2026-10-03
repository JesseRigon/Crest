using Crest.Money.Abstractions;
using Crest.Money.Fields;
using Crest.Money.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace Crest.Money;

[Feature(MoneyConstants.FeatureId)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentField<PriceField>();

        // Global-store currency table first (a shared correction to minor units wins),
        // the culture-derived table behind it for anything the store lacks.
        services.AddSingleton<GlobalCurrencyProvider>();
        services.AddSingleton<ICurrencyProvider>(sp => sp.GetRequiredService<GlobalCurrencyProvider>());
        services.AddScoped<IModularTenantEvents, GlobalCurrencyProviderLoader>();
        services.AddSingleton<ICurrencyProvider, CurrencyProvider>();
        services.AddScoped<IMoneyService, MoneyService>();
    }
}
