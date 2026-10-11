using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc.Rendering;
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

public sealed class TokenBucketRateLimiterDisplayDriver : DisplayDriver<RateLimitLimiter>
{
    private readonly IRateLimiterSource _source;
    private readonly IStringLocalizer S;

    public TokenBucketRateLimiterDisplayDriver(
        [FromKeyedServices(TokenBucketRateLimiterSource.SourceName)] IRateLimiterSource source,
        IStringLocalizer<TokenBucketRateLimiterDisplayDriver> stringLocalizer)
    {
        _source = source;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(RateLimitLimiter limiter, BuildEditorContext context)
    {
        if (limiter.Source != TokenBucketRateLimiterSource.SourceName)
        {
            return null;
        }

        return Initialize<TokenBucketRateLimiterViewModel>("TokenBucketRateLimiter_Edit", model =>
        {
            var data = limiter.GetOrCreate<TokenBucketRateLimiterData>();
            model.TokenLimit = data.TokenLimit;
            model.QueueLimit = data.QueueLimit;
            model.TokensPerPeriod = data.TokensPerPeriod;
            model.ReplenishmentPeriodSeconds = data.ReplenishmentPeriodSeconds;
            model.QueueProcessingOrder = data.QueueProcessingOrder;
            model.QueueProcessingOrders = GetQueueProcessingOrders();
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(RateLimitLimiter limiter, UpdateEditorContext context)
    {
        if (limiter.Source != TokenBucketRateLimiterSource.SourceName)
        {
            return null;
        }

        var model = new TokenBucketRateLimiterViewModel
        {
            QueueProcessingOrders = GetQueueProcessingOrders(),
        };

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.TokenLimit < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TokenLimit), S["Token limit must be greater than zero."]);
        }

        if (model.TokensPerPeriod < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TokensPerPeriod), S["Tokens per period must be greater than zero."]);
        }

        if (model.ReplenishmentPeriodSeconds < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ReplenishmentPeriodSeconds), S["Replenishment period must be greater than zero."]);
        }

        limiter.Put(new TokenBucketRateLimiterData
        {
            TokenLimit = model.TokenLimit,
            QueueLimit = model.QueueLimit,
            TokensPerPeriod = model.TokensPerPeriod,
            ReplenishmentPeriodSeconds = model.ReplenishmentPeriodSeconds,
            QueueProcessingOrder = model.QueueProcessingOrder,
        });

        return Edit(limiter, context);
    }

    public override IDisplayResult Display(RateLimitLimiter limiter, BuildDisplayContext context)
    {
        if (limiter.Source != TokenBucketRateLimiterSource.SourceName)
        {
            return null;
        }

        return Combine(
            Initialize<RateLimiterSourceViewModel>("TokenBucketRateLimiter_SummaryAdmin", model =>
            {
                model.Name = _source.Name;
                model.DisplayName = _source.DisplayName.Value;
                model.Description = _source.Describe(limiter);
            }).Location(PlatformConstants.DisplayType.SummaryAdmin, "Content:5"));
    }

    private SelectListItem[] GetQueueProcessingOrders()
    {
        return
        [
            new SelectListItem
            {
                Text = S["Oldest first (first come, first served)"],
                Value = nameof(QueueProcessingOrder.OldestFirst),
            },
            new SelectListItem
            {
                Text = S["Newest first (latest request first)"],
                Value = nameof(QueueProcessingOrder.NewestFirst),
            },
        ];
    }
}
