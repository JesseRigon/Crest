using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Grove.Security.RateLimits.Models;
using Grove.Infrastructure.Settings;

namespace Grove.Security.RateLimits.Settings;

/// <summary>
/// The rate limits settings form: the tenant's rate-limit policies (the current version of each),
/// stored in <see cref="IRateLimitPolicyStore"/> by <see cref="RateLimitsSettingsStore"/>.
/// </summary>
public sealed class RateLimitsSettingsModel
{
    /// <summary>The settings group of the rate limits.</summary>
    public const string GroupId = "ratelimits";

    [Display(Name = "Policies", Description = "The tenant's rate-limit policies. An enabled policy is enforced; while it stays enabled, its type, target and limiters cannot change (disable it first, or create another policy).")]
    public List<RateLimitPolicyModel> Policies { get; set; } = [];
}

/// <summary>One rate-limit policy of the form.</summary>
public sealed class RateLimitPolicyModel
{
    [Display(Name = "Policy name", Description = "Use a unique name to identify this tenant policy.", Order = 0)]
    [Required]
    public string Name { get; set; }

    [Display(Name = "Description", Description = "Optionally describe what this policy is intended to protect.", Order = 1)]
    public string Description { get; set; }

    [Display(Name = "Enabled", Description = "An enabled policy is enforced at runtime; enabling or disabling a policy reloads the tenant.", Order = 2)]
    public bool IsEnabled { get; set; }

    [Display(Name = "Policy type", Description = "Global applies everywhere, Endpoint matches a request path prefix, and Group matches endpoints assigned to a named rate-limit group.", Order = 3)]
    public RateLimitPolicyScope Scope { get; set; }

    [Display(Name = "Request path", Description = "Matches requests that start with this path, for example '/api/' or '/users/login'.", Order = 4)]
    [Setting(VisibleWhen = "Scope=Endpoint")]
    public string Path { get; set; }

    [Display(Name = "Rate-limit group", Description = "Choose a group declared on one or more endpoints with the RateLimitGroup attribute or WithRateLimitGroups().", Order = 5)]
    [Setting(VisibleWhen = "Scope=Group", Options = typeof(RateLimitGroupOptions))]
    public string GroupName { get; set; }

    [Display(Name = "Limiters", Description = "The limiters applied when the policy matches a request.", Order = 6)]
    public List<RateLimiterModel> Limiters { get; set; } = [];

    [Display(Name = "Enabled since", Order = 7)]
    [Setting(ReadOnly = true)]
    public string EnabledSince { get; set; }

    [Display(Name = "Author", Order = 8)]
    [Setting(ReadOnly = true)]
    public string Author { get; set; }

    /// <summary>
    /// The stored policy's identifier, sent back with the form so a saved policy is updated rather
    /// than recreated; empty for a new policy.
    /// </summary>
    [Setting(Editor = "hidden")]
    public string Id { get; set; }
}

/// <summary>The rate limiter kinds, named as their <see cref="IRateLimiterSource"/>.</summary>
public enum RateLimiterKind
{
    [Display(Name = "Fixed window")]
    FixedWindow,

    [Display(Name = "Sliding window")]
    SlidingWindow,

    [Display(Name = "Token bucket")]
    TokenBucket,

    [Display(Name = "Concurrency")]
    Concurrency,
}

/// <summary>One limiter of a policy, its kind's fields flattened.</summary>
public sealed class RateLimiterModel
{
    [Display(Name = "Kind", Description = "Fixed window allows a set number of requests during each time period, then resets the count all at once. Sliding window spreads the limit across a rolling time period. Token bucket allows short bursts while steadily refilling capacity. Concurrency limits how many matching requests can run at the same time.", Order = 0)]
    public RateLimiterKind Kind { get; set; }

    [Display(Name = "Permit limit", Description = "How many requests can succeed during one window (fixed or sliding), or run at the same time (concurrency), before extra requests are delayed or rejected.", Order = 1)]
    [Setting(VisibleWhen = "Kind=FixedWindow|SlidingWindow|Concurrency")]
    public int PermitLimit { get; set; }

    [Display(Name = "Window seconds", Description = "How long one window lasts. For example, 60 means the last minute of traffic is counted.", Order = 2)]
    [Setting(VisibleWhen = "Kind=FixedWindow|SlidingWindow")]
    public int WindowSeconds { get; set; }

    [Display(Name = "Segments per window", Description = "How many smaller slices are tracked inside the window. More slices make the limit recover more gradually instead of all at once.", Order = 3)]
    [Setting(VisibleWhen = "Kind=SlidingWindow")]
    public int SegmentsPerWindow { get; set; }

    [Display(Name = "Token limit", Description = "The bucket size. This is the largest burst of requests a client can use at once before it must wait for tokens to refill.", Order = 4)]
    [Setting(VisibleWhen = "Kind=TokenBucket")]
    public int TokenLimit { get; set; }

    [Display(Name = "Tokens per period", Description = "How many requests' worth of capacity are added back each refill period.", Order = 5)]
    [Setting(VisibleWhen = "Kind=TokenBucket")]
    public int TokensPerPeriod { get; set; }

    [Display(Name = "Replenishment seconds", Description = "How often tokens are added back. For example, 10 with 5 tokens per period adds 5 more tokens every 10 seconds.", Order = 6)]
    [Setting(VisibleWhen = "Kind=TokenBucket")]
    public int ReplenishmentPeriodSeconds { get; set; }

    [Display(Name = "Queue limit", Description = "How many extra requests can wait after the limit is reached. Use 0 to reject extras immediately instead of making them wait.", Order = 7)]
    public int QueueLimit { get; set; }

    [Display(Name = "Queue processing order", Description = "Whether waiting requests are handled in arrival order, or the newest waiting request goes first.", Order = 8)]
    [Setting(VisibleWhen = "Kind=TokenBucket|Concurrency")]
    public QueueProcessingOrder QueueProcessingOrder { get; set; }
}
