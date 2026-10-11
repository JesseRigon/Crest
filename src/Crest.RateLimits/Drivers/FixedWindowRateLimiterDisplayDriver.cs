using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Entities;
using Crest.Mvc.ModelBinding;
using Crest.RateLimits;
using Crest.RateLimits.Models;
using Crest.RateLimits.Services;
using Crest.RateLimits.ViewModels;

namespace Crest.RateLimits.Drivers;

public sealed class FixedWindowRateLimiterDisplayDriver : DisplayDriver<RateLimitLimiter>
{
    private readonly IRateLimiterSource _source;
    private readonly IStringLocalizer S;

    public FixedWindowRateLimiterDisplayDriver(
        [FromKeyedServices(FixedWindowRateLimiterSource.SourceName)] IRateLimiterSource source,
        IStringLocalizer<FixedWindowRateLimiterDisplayDriver> stringLocalizer)
    {
        _source = source;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(RateLimitLimiter limiter, BuildEditorContext context)
    {
        if (limiter.Source != FixedWindowRateLimiterSource.SourceName)
        {
            return null;
        }

        return Initialize<FixedWindowRateLimiterViewModel>("FixedWindowRateLimiter_Edit", model =>
        {
            var data = limiter.GetOrCreate<FixedWindowRateLimiterData>();
            model.PermitLimit = data.PermitLimit;
            model.QueueLimit = data.QueueLimit;
            model.WindowSeconds = data.WindowSeconds;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(RateLimitLimiter limiter, UpdateEditorContext context)
    {
        if (limiter.Source != FixedWindowRateLimiterSource.SourceName)
        {
            return null;
        }

        var model = new FixedWindowRateLimiterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.PermitLimit < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PermitLimit), S["Permit limit must be greater than zero."]);
        }

        if (model.WindowSeconds < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.WindowSeconds), S["Window must be greater than zero."]);
        }

        limiter.Put(new FixedWindowRateLimiterData
        {
            PermitLimit = model.PermitLimit,
            QueueLimit = model.QueueLimit,
            WindowSeconds = model.WindowSeconds,
        });

        return Edit(limiter, context);
    }

    public override IDisplayResult Display(RateLimitLimiter limiter, BuildDisplayContext context)
    {
        if (limiter.Source != FixedWindowRateLimiterSource.SourceName)
        {
            return null;
        }

        return Combine(
            Initialize<RateLimiterSourceViewModel>("FixedWindowRateLimiter_SummaryAdmin", model =>
            {
                model.Name = _source.Name;
                model.DisplayName = _source.DisplayName.Value;
                model.Description = _source.Describe(limiter);
            }).Location(PlatformConstants.DisplayType.SummaryAdmin, "Content:5"));
    }
}
