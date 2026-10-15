namespace Grove.Data.Types.Money.Settings;

// Bound from configuration for now; the tenant-level currency settings surface
// (a Grove settings page writing site settings) is a later slice - this class is
// the seam it will fill.
public class CurrencySettings
{
    public string? DefaultCurrency { get; set; }
}
