using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.Entities;
using Grove.Environment.Shell;
using Grove.RateLimits.Models;
using Grove.RateLimits.Services;
using Grove.Settings;
using Grove.Validation;

namespace Grove.RateLimits.Settings;

/// <summary>
/// Maps the rate limits settings form onto <see cref="IRateLimitPolicyStore"/>: loads the current
/// version of every policy, and saves by creating, updating and deleting policies until the store
/// equals the form, with the rules of the former policy and limiter editors.
/// </summary>
public static class RateLimitsSettingsStore
{
    private const string PublishedPolicyMessage = "This policy is enabled and cannot be modified. To modify it, disable it first or create another policy.";

    public static async Task<RateLimitsSettingsModel> LoadAsync(IServiceProvider services)
    {
        var policies = await services.GetRequiredService<IRateLimitPolicyStore>().GetAllAsync(PolicyVersion.Current);

        return new RateLimitsSettingsModel
        {
            Policies = [.. policies.OrderBy(policy => policy.Name, StringComparer.OrdinalIgnoreCase).Select(ToModel)],
        };
    }

    public static async Task ValidateAsync(IServiceProvider services, RateLimitsSettingsModel model, ValidationErrors errors)
    {
        var existing = (await services.GetRequiredService<IRateLimitPolicyStore>().GetAllAsync(PolicyVersion.Current))
            .ToDictionary(policy => policy.PolicyId, StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var policies = model.Policies ?? [];

        for (var index = 0; index < policies.Count; index++)
        {
            var item = policies[index];
            var key = $"policies[{index}]";

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                errors.Add($"{key}.name", "The policy name is required.");
            }
            else if (!names.Add(item.Name.Trim()))
            {
                errors.Add($"{key}.name", "A policy with the same name already exists.");
            }

            RateLimitPolicy current = null;
            if (!string.IsNullOrWhiteSpace(item.Id))
            {
                if (!existing.TryGetValue(item.Id.Trim(), out current))
                {
                    errors.Add($"{key}.id", "This policy no longer exists; reload the page.");
                }
                else if (!ids.Add(current.PolicyId))
                {
                    errors.Add($"{key}.id", "Two policies have the same id.");
                }
            }

            if (current is { IsEnabled: true } && item.IsEnabled)
            {
                // An enabled policy keeps its target and limiters until it is disabled.
                var updated = ToPolicy(item, current, userId: null, userName: null, keepEnabledTarget: false);
                if (updated.Scope != current.Scope || !string.Equals(updated.Path, current.Path, StringComparison.Ordinal)
                    || !string.Equals(updated.GroupName, current.GroupName, StringComparison.Ordinal))
                {
                    errors.Add($"{key}.scope", PublishedPolicyMessage);
                }

                if (!SameLimiters(updated.Limiters, current.Limiters))
                {
                    errors.Add($"{key}.limiters", PublishedPolicyMessage);
                }
            }

            switch (item.Scope)
            {
                case RateLimitPolicyScope.Endpoint when string.IsNullOrWhiteSpace(item.Path):
                    errors.Add($"{key}.path", "A request path is required for endpoint policies.");
                    break;
                case RateLimitPolicyScope.Endpoint when !item.Path.Trim().StartsWith('/'):
                    errors.Add($"{key}.path", "The request path must start with '/'.");
                    break;
                case RateLimitPolicyScope.Group when string.IsNullOrWhiteSpace(item.GroupName):
                    errors.Add($"{key}.groupName", "A rate-limit group is required for group policies.");
                    break;
            }

            var limiters = item.Limiters ?? [];
            for (var limiterIndex = 0; limiterIndex < limiters.Count; limiterIndex++)
            {
                ValidateLimiter(limiters[limiterIndex], $"{key}.limiters[{limiterIndex}]", errors);
            }
        }
    }

    public static async Task SaveAsync(IServiceProvider services, RateLimitsSettingsModel model)
    {
        var store = services.GetRequiredService<IRateLimitPolicyStore>();
        var existing = (await store.GetAllAsync(PolicyVersion.Current)).ToDictionary(policy => policy.PolicyId, StringComparer.Ordinal);
        var user = services.GetService<IHttpContextAccessor>()?.HttpContext?.User;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = user?.Identity?.Name;
        var kept = new HashSet<string>(StringComparer.Ordinal);
        var reload = false;

        foreach (var item in model.Policies ?? [])
        {
            if (!string.IsNullOrWhiteSpace(item.Id) && existing.TryGetValue(item.Id.Trim(), out var current))
            {
                kept.Add(current.PolicyId);
                var updated = ToPolicy(item, current, userId, userName);
                if (!string.Equals(Serialize(updated), Serialize(current), StringComparison.Ordinal))
                {
                    await store.UpdateAsync(updated);
                }

                reload |= updated.IsEnabled != current.IsEnabled;
                item.Id = updated.PolicyId;
                item.EnabledSince = FormatEnabled(updated.EnabledUtc);
            }
            else
            {
                var created = ToPolicy(item, current: null, userId, userName);
                await store.CreateAsync(created);
                reload |= created.IsEnabled;
                item.Id = created.PolicyId;
                item.Author = created.Author;
                item.EnabledSince = FormatEnabled(created.EnabledUtc);
            }
        }

        foreach (var removed in existing.Values.Where(policy => !kept.Contains(policy.PolicyId)))
        {
            if (await store.DeleteAsync(removed))
            {
                reload |= removed.IsEnabled;
            }
        }

        // The enforced policies are read when the tenant is built: enabling, disabling or deleting
        // an enabled policy reloads it.
        if (reload)
        {
            services.GetRequiredService<IShellReleaseManager>().RequestRelease();
        }
    }

    private static RateLimitPolicyModel ToModel(RateLimitPolicy policy) => new()
    {
        Id = policy.PolicyId,
        Name = policy.Name,
        Description = policy.Description,
        IsEnabled = policy.IsEnabled,
        Scope = policy.Scope,
        Path = policy.Path,
        GroupName = policy.GroupName,
        Author = policy.Author,
        EnabledSince = FormatEnabled(policy.EnabledUtc),
        Limiters = [.. policy.Limiters.Select(ToModel).Where(limiter => limiter is not null)],
    };

    private static RateLimiterModel ToModel(RateLimitLimiter limiter)
    {
        if (!Enum.TryParse<RateLimiterKind>(limiter.Source, out var kind))
        {
            return null;
        }

        var model = new RateLimiterModel { Kind = kind };
        switch (kind)
        {
            case RateLimiterKind.FixedWindow:
                var fixedWindow = limiter.GetOrCreate<FixedWindowRateLimiterData>();
                (model.PermitLimit, model.QueueLimit, model.WindowSeconds) = (fixedWindow.PermitLimit, fixedWindow.QueueLimit, fixedWindow.WindowSeconds);
                break;
            case RateLimiterKind.SlidingWindow:
                var slidingWindow = limiter.GetOrCreate<SlidingWindowRateLimiterData>();
                (model.PermitLimit, model.QueueLimit, model.WindowSeconds, model.SegmentsPerWindow) =
                    (slidingWindow.PermitLimit, slidingWindow.QueueLimit, slidingWindow.WindowSeconds, slidingWindow.SegmentsPerWindow);
                break;
            case RateLimiterKind.TokenBucket:
                var tokenBucket = limiter.GetOrCreate<TokenBucketRateLimiterData>();
                (model.TokenLimit, model.QueueLimit, model.TokensPerPeriod, model.ReplenishmentPeriodSeconds, model.QueueProcessingOrder) =
                    (tokenBucket.TokenLimit, tokenBucket.QueueLimit, tokenBucket.TokensPerPeriod, tokenBucket.ReplenishmentPeriodSeconds, tokenBucket.QueueProcessingOrder);
                break;
            case RateLimiterKind.Concurrency:
                var concurrency = limiter.GetOrCreate<ConcurrencyRateLimiterData>();
                (model.PermitLimit, model.QueueLimit, model.QueueProcessingOrder) = (concurrency.PermitLimit, concurrency.QueueLimit, concurrency.QueueProcessingOrder);
                break;
        }

        return model;
    }

    /// <summary>
    /// The stored policy for a form item: a new one (fresh id, the current user as owner), or
    /// <paramref name="current"/> changed, whose target and limiters stay while it stays enabled.
    /// </summary>
    private static RateLimitPolicy ToPolicy(RateLimitPolicyModel item, RateLimitPolicy current, string userId, string userName, bool keepEnabledTarget = true)
    {
        var keepTarget = keepEnabledTarget && current is { IsEnabled: true } && item.IsEnabled;
        var policy = new RateLimitPolicy
        {
            PolicyId = current?.PolicyId ?? IdGenerator.GenerateId(),
            Name = item.Name?.Trim(),
            Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
            OwnerId = current is null ? userId : current.OwnerId,
            Author = current is null ? userName : current.Author,
            Scope = keepTarget ? current.Scope : item.Scope,
            Path = keepTarget ? current.Path : item.Scope == RateLimitPolicyScope.Endpoint ? item.Path?.Trim() : null,
            GroupName = keepTarget ? current.GroupName : item.Scope == RateLimitPolicyScope.Group ? item.GroupName?.Trim() : null,
            IsEnabled = item.IsEnabled,
            EnabledUtc = item.IsEnabled
                ? current is { IsEnabled: true } ? current.EnabledUtc ?? DateTime.UtcNow : DateTime.UtcNow
                : null,
            Status = RateLimitPolicyStore.GetStatus(item.IsEnabled),
        };

        if (keepTarget)
        {
            policy.Limiters.AddRange(current.Limiters);
            return policy;
        }

        var limiters = item.Limiters ?? [];
        for (var index = 0; index < limiters.Count; index++)
        {
            // A limiter keeps its id while its position and kind stay.
            var previous = current is not null && index < current.Limiters.Count ? current.Limiters[index] : null;
            var source = limiters[index].Kind.ToString();
            policy.Limiters.Add(ToLimiter(limiters[index], previous?.Source == source ? previous.Id : IdGenerator.GenerateId()));
        }

        return policy;
    }

    private static RateLimitLimiter ToLimiter(RateLimiterModel model, string id)
    {
        var limiter = new RateLimitLimiter { Id = id, Source = model.Kind.ToString() };
        switch (model.Kind)
        {
            case RateLimiterKind.FixedWindow:
                limiter.Put(new FixedWindowRateLimiterData { PermitLimit = model.PermitLimit, QueueLimit = model.QueueLimit, WindowSeconds = model.WindowSeconds });
                break;
            case RateLimiterKind.SlidingWindow:
                limiter.Put(new SlidingWindowRateLimiterData { PermitLimit = model.PermitLimit, QueueLimit = model.QueueLimit, WindowSeconds = model.WindowSeconds, SegmentsPerWindow = model.SegmentsPerWindow });
                break;
            case RateLimiterKind.TokenBucket:
                limiter.Put(new TokenBucketRateLimiterData
                {
                    TokenLimit = model.TokenLimit,
                    QueueLimit = model.QueueLimit,
                    TokensPerPeriod = model.TokensPerPeriod,
                    ReplenishmentPeriodSeconds = model.ReplenishmentPeriodSeconds,
                    QueueProcessingOrder = model.QueueProcessingOrder,
                });
                break;
            case RateLimiterKind.Concurrency:
                limiter.Put(new ConcurrencyRateLimiterData { PermitLimit = model.PermitLimit, QueueLimit = model.QueueLimit, QueueProcessingOrder = model.QueueProcessingOrder });
                break;
        }

        return limiter;
    }

    private static void ValidateLimiter(RateLimiterModel limiter, string key, ValidationErrors errors)
    {
        switch (limiter.Kind)
        {
            case RateLimiterKind.FixedWindow or RateLimiterKind.SlidingWindow or RateLimiterKind.Concurrency when limiter.PermitLimit < 1:
                errors.Add($"{key}.permitLimit", "Permit limit must be greater than zero.");
                break;
        }

        if (limiter.Kind is RateLimiterKind.FixedWindow or RateLimiterKind.SlidingWindow && limiter.WindowSeconds < 1)
        {
            errors.Add($"{key}.windowSeconds", "Window must be greater than zero.");
        }

        if (limiter.Kind == RateLimiterKind.SlidingWindow && limiter.SegmentsPerWindow < 1)
        {
            errors.Add($"{key}.segmentsPerWindow", "Segments per window must be greater than zero.");
        }

        if (limiter.Kind == RateLimiterKind.TokenBucket)
        {
            if (limiter.TokenLimit < 1)
            {
                errors.Add($"{key}.tokenLimit", "Token limit must be greater than zero.");
            }

            if (limiter.TokensPerPeriod < 1)
            {
                errors.Add($"{key}.tokensPerPeriod", "Tokens per period must be greater than zero.");
            }

            if (limiter.ReplenishmentPeriodSeconds < 1)
            {
                errors.Add($"{key}.replenishmentPeriodSeconds", "Replenishment period must be greater than zero.");
            }
        }

        if (limiter.QueueLimit < 0)
        {
            errors.Add($"{key}.queueLimit", "Queue limit cannot be negative.");
        }
    }

    private static bool SameLimiters(List<RateLimitLimiter> left, List<RateLimitLimiter> right) =>
        left.Count == right.Count
        && left.Zip(right).All(pair => string.Equals(pair.First.Source, pair.Second.Source, StringComparison.Ordinal)
            && string.Equals(pair.First.Properties?.ToJsonString(), pair.Second.Properties?.ToJsonString(), StringComparison.Ordinal));

    private static string Serialize(RateLimitPolicy policy) => JsonSerializer.Serialize(policy);

    private static string FormatEnabled(DateTime? enabledUtc) =>
        enabledUtc?.ToString("u", CultureInfo.InvariantCulture);
}

/// <summary>
/// The rate-limit groups declared on endpoints (<see cref="IRateLimitGroupMetadata"/>) and those the
/// stored policies use: the choices of a group policy's target.
/// </summary>
public sealed class RateLimitGroupOptions(EndpointDataSource endpointDataSource, IRateLimitPolicyStore policyStore) : ISettingOptions
{
    public async Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var used = (await policyStore.GetAllAsync(PolicyVersion.Current)).Select(policy => policy.GroupName);

        return endpointDataSource.Endpoints
            .SelectMany(endpoint => endpoint.Metadata.OfType<IRateLimitGroupMetadata>().SelectMany(metadata => metadata.GroupNames))
            .Concat(used)
            .Where(groupName => !string.IsNullOrWhiteSpace(groupName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(groupName => groupName, StringComparer.OrdinalIgnoreCase)
            .Select(groupName => new SettingOption(groupName, groupName))
            .ToArray();
    }
}
