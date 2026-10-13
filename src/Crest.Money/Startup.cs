using Crest.Money.Abstractions;
using Crest.Money.Fields;
using Crest.Money.Services;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.Data.Types;
using Crest.Modules;

namespace Crest.Money;

[Feature(MoneyConstants.FeatureId)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentField<PriceField>();

        // Money owns its value type and registers it with the one value type registry; no
        // editor or display primitive is registered for it yet.
        services.AddValueType(new Crest.Data.Types.ValueTypeDescriptor(
            "Money", Crest.Data.Types.ValueTypeKinds.Object, Crest.Data.Types.ValueTypeKinds.Object, [nameof(PriceField)],
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
        services.AddScoped<Crest.Services.IOptionSourceProvider, CurrencyOptionSourceProvider>();
    }
}
