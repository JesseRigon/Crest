using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Entities;
using Crest.Mvc.ModelBinding;
using Crest.RateLimits.Core;
using Crest.RateLimits.Models;
using Crest.RateLimits.Services;
using Crest.RateLimits.ViewModels;

namespace Crest.RateLimits.Drivers;

public sealed class ConcurrencyRateLimiterDisplayDriver : DisplayDriver<RateLimitLimiter>
{
    private readonly IRateLimiterSource _source;
    private readonly IStringLocalizer<ConcurrencyRateLimiterDisplayDriver> S;

    public ConcurrencyRateLimiterDisplayDriver(
        [FromKeyedServices(ConcurrencyRateLimiterSource.SourceName)] IRateLimiterSource source,
        IStringLocalizer<ConcurrencyRateLimiterDisplayDriver> stringLocalizer)
    {
        _source = source;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(RateLimitLimiter limiter, BuildEditorContext context)
    {
        if (limiter.Source != ConcurrencyRateLimiterSource.SourceName)
        {
            return null;
        }

        return Initialize<ConcurrencyRateLimiterViewModel>("ConcurrencyRateLimiter_Edit", model =>
        {
            var data = limiter.GetOrCreate<ConcurrencyRateLimiterData>();
            model.PermitLimit = data.PermitLimit;
            model.QueueLimit = data.QueueLimit;
            model.QueueProcessingOrder = data.QueueProcessingOrder;
            model.QueueProcessingOrders = GetQueueProcessingOrders();
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(RateLimitLimiter limiter, UpdateEditorContext context)
    {
        if (limiter.Source != ConcurrencyRateLimiterSource.SourceName)
        {
            return null;
        }

        var model = new ConcurrencyRateLimiterViewModel
        {
            QueueProcessingOrders = GetQueueProcessingOrders(),
        };

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.PermitLimit < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PermitLimit), S["Permit limit must be greater than zero."]);
        }

        limiter.Put(new ConcurrencyRateLimiterData
        {
            PermitLimit = model.PermitLimit,
            QueueLimit = model.QueueLimit,
            QueueProcessingOrder = model.QueueProcessingOrder,
        });

        return Edit(limiter, context);
    }

    public override IDisplayResult Display(RateLimitLimiter limiter, BuildDisplayContext context)
    {
        if (limiter.Source != ConcurrencyRateLimiterSource.SourceName)
        {
            return null;
        }

        return Combine(
            Initialize<RateLimiterSourceViewModel>("ConcurrencyRateLimiter_SummaryAdmin", model =>
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
