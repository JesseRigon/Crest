namespace Grove.Data.Types.Money.Settings;

/// <summary>
/// The tenant's exchange-rate settings (site settings): its own rate table, which
/// <see cref="Services.ManualExchangeRateProvider"/> reads, and what a missing rate costs.
/// </summary>
public sealed class ExchangeRateSettings
{
    /// <summary>One of <see cref="ExchangeRateFailureBehaviors"/>.</summary>
    public string FailureBehavior { get; set; } = ExchangeRateFailureBehaviors.Continue;

    /// <summary>The tenant's manual rates. Effective-dated: the latest on or before the date wins; the inverse of a stated rate is a stated rate.</summary>
    public List<ManualExchangeRate> Rates { get; set; } = [];
}

/// <summary>One row of the tenant's rate table: one unit of <see cref="From"/> is worth <see cref="Rate"/> units of <see cref="To"/> from <see cref="EffectiveFrom"/> on.</summary>
public sealed class ManualExchangeRate
{
    /// <summary>The denomination code (an ISO 4217 code for a currency).</summary>
    public string From { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public DateOnly EffectiveFrom { get; set; }
}

/// <summary>What happens when no exchange-rate provider answers.</summary>
public static class ExchangeRateFailureBehaviors
{
    /// <summary>Carry on without a rate: the registry answers null and the caller decides.</summary>
    public const string Continue = "Continue";

    /// <summary>Refuse: the registry throws <see cref="Abstractions.ExchangeRateUnavailableException"/>.</summary>
    public const string Block = "Block";
}
