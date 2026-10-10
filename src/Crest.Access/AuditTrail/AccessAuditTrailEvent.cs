using Crest.AuditTrail.Services.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Crest.Access.AuditTrail;

public sealed class AccessAuditTrailEvent
{
    public string Operation { get; set; } = string.Empty;

    public string Verdict { get; set; } = string.Empty;

    public string Side { get; set; } = string.Empty;

    public string? OrganizationId { get; set; }

    public string? ImpersonatorUserId { get; set; }

    public bool IsSystem { get; set; }

    public bool IsRead { get; set; }

    public string? Resource { get; set; }

    public string? ScopeSignature { get; set; }

    public string? Reason { get; set; }

    public double? DurationMilliseconds { get; set; }
}

public sealed class AccessAuditTrailEventConfiguration : IConfigureOptions<AuditTrailOptions>
{
    public const string Category = "Access";

    public const string Allowed = "Allowed";

    public const string Denied = "Denied";

    public const string Executed = "Executed";

    public void Configure(AuditTrailOptions options)
    {
        options.For<AccessAuditTrailEventConfiguration>(Category, S => S["Access"])
            .WithEvent(Denied, S => S["Denied"], S => S["A caller was denied an operation."], true)
            .WithEvent(Executed, S => S["Executed"], S => S["An operation ran for a caller."], true)
            .WithEvent(Allowed, S => S["Allowed"], S => S["A caller was allowed an operation."], false);
    }
}
