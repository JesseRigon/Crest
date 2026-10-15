using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;
using TimeZoneConverter;

namespace Grove.Display.Liquid;

public static class LiquidTemplateContextExtensions
{
    /// <summary>Sets the tenant's local time zone, local now and the current UI culture, once per context.</summary>
    public static async ValueTask InitializeAsync(this LiquidTemplateContext context)
    {
        if (context.IsInitialized)
        {
            return;
        }

        // The tenant's clock, where there is one (a scope without the platform's services renders
        // with the process's time zone and clock).
        if (context.Services.GetService<ILocalClock>() is { } localClock)
        {
            var localTimeZone = await localClock.GetLocalTimeZoneAsync();
            if (TZConvert.TryGetTimeZoneInfo(localTimeZone.TimeZoneId, out var timeZoneInfo))
            {
                context.TimeZone = timeZoneInfo;
            }

            var now = await localClock.GetLocalNowAsync();
            context.Now = () => now;
        }

        context.CultureInfo = CultureInfo.CurrentUICulture;
        context.IsInitialized = true;
    }
}
