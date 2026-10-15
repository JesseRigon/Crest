using Grove.Money.Abstractions;
using Grove.Money.Fields;
using Grove.Money.Services;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.Data.Types;
using Grove.Modules;

namespace Grove.Money;

[Feature(MoneyConstants.FeatureId)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentField<PriceField>();

        // Money owns its value type and registers it with the one value type registry; no
        // editor or display primitive is registered for it yet.
        services.AddValueType(new Grove.Data.Types.ValueTypeDescriptor(
            "Money", Grove.Data.Types.ValueTypeKinds.Object, Grove.Data.Types.ValueTypeKinds.Object, [nameof(PriceField)],
            ClrType: typeof(Amount), DisplayName: "Money", Module: MoneyConstants.FeatureId));

        // Global-store currency table first (a shared correction to minor units wins),
        // the culture-derived table behind it for anything the store lacks.
        services.AddSingleton<GlobalCurrencyProvider>();
        services.AddSingleton<ICurrencyProvider>(sp => sp.GetRequiredService<GlobalCurrencyProvider>());
        services.AddScoped<IModularTenantEvents, GlobalCurrencyProviderLoader>();
        services.AddSingleton<ICurrencyProvider, CurrencyProvider>();
        services.AddScoped<IMoneyService, MoneyService>();

        // Exchange rates: the tenant's own table at the lowest priority, feeds above it; the
        // registry is what callers inject (docs/types.md › Denominations).
        services.AddScoped<IExchangeRateProvider, ManualExchangeRateProvider>();
        services.AddScoped<IExchangeRateRegistry, ExchangeRateRegistry>();

        // Pickers over the currency system: the `currency` option source.
        services.AddScoped<Grove.Services.IOptionSourceProvider, CurrencyOptionSourceProvider>();
    }
}
