using Grove.Access;
using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services;
using Grove.AuditTrail.Services.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Members.AuditTrail;

/// <summary>The Members audit category (docs/audit.md › step 3: members and bindings).</summary>
public sealed class MemberAuditTrailEventConfiguration : IConfigureOptions<AuditTrailOptions>
{
    public const string Category = "Members";

    public const string BindingAdded = nameof(BindingAdded);

    public const string BindingRemoved = nameof(BindingRemoved);

    public const string ActiveOrganizationChanged = nameof(ActiveOrganizationChanged);

    public const string OwnershipTransferred = nameof(OwnershipTransferred);

    public void Configure(AuditTrailOptions options)
    {
        options.For<MemberAuditTrailEventConfiguration>(Category, S => S["Members"])
            .WithEvent(BindingAdded, S => S["Binding added"], S => S["A member was bound to an organization."], true)
            .WithEvent(BindingRemoved, S => S["Binding removed"], S => S["A member's binding to an organization was removed."], true)
            .WithEvent(ActiveOrganizationChanged, S => S["Organization switched"], S => S["A member session switched its active organization."], false)
            .WithEvent(OwnershipTransferred, S => S["Ownership transferred"], S => S["An organization's ownership moved to another user."], true);
    }
}

/// <summary>The data of a Members event.</summary>
public sealed class MemberAuditTrailEvent
{
    public string? MemberUserId { get; set; }

    public string? MemberUserName { get; set; }

    public string? OrganizationId { get; set; }

    public List<string> Roles { get; set; } = [];

    public bool IsMemberAdmin { get; set; }

    /// <summary>On an ownership transfer: the owner before it.</summary>
    public string? PreviousOwnerUserId { get; set; }
}

/// <summary>
/// Records Members' events through <see cref="IAuditTrailManager"/> when the audit trail is
/// enabled, and does nothing when it is not: Members never requires the audit trail.
/// </summary>
public sealed class MemberAuditRecorder(IServiceProvider services, ICallerContextAccessor callers)
{
    public Task RecordAsync(string category, string name, string correlationId, MemberAuditTrailEvent data, AuditTrailSubject? subject = null, AuditTrailAttribution? attribution = null, string? userId = null, string? userName = null)
    {
        var manager = services.GetService<IAuditTrailManager>();
        if (manager is null)
        {
            return Task.CompletedTask;
        }

        var caller = callers.Current;
        return manager.RecordEventAsync(new AuditTrailContext<MemberAuditTrailEvent>(
            name,
            category,
            correlationId,
            userId ?? caller?.UserId ?? string.Empty,
            userName ?? caller?.UserName ?? string.Empty,
            data)
        {
            Subject = subject,
            Attribution = attribution,
        });
    }
}
