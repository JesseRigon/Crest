// Adapted from OrchardCore.Commerce (MIT, Copyright (c) 2018 Crest).
namespace Crest.Money.Fields;

public class PriceFieldSettings
{
    public string? Hint { get; set; }
    public string? Label { get; set; }
    public bool Required { get; set; }

    public CurrencySelectionMode CurrencySelectionMode { get; set; }
    public string? SpecificCurrencyIsoCode { get; set; }
}

public enum CurrencySelectionMode
{
    AllCurrencies,
    DefaultCurrency,
    SpecificCurrency,
}
