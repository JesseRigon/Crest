using Crest.Access;
using Crest.AuditTrail.Models;
using Crest.AuditTrail.Services.Models;
using Crest.Entities;
using Crest.Members.AuditTrail;
using Crest.Members.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Crest.Members.Tests;

/// <summary>
/// docs/audit.md › step 1: a content item created by staff while impersonating keeps the member
/// as its owner and author (the stock content handler takes the user from the member principal),
/// and its audit event names the staff user, the member and the organization.
/// </summary>
public class ImpersonationAuditTests
{
    private sealed class Callers(CallerContext? current) : ICallerContextAccessor
    {
        public CallerContext? Current { get; set; } = current;
    }

    private static CallerContext ImpersonatedMember() => new()
    {
        Tenant = "Default",
        Side = CallerSide.Member,
        UserId = "member-1",
        UserName = "mia",
        UserClass = "member",
        OrganizationId = "org-1",
        ImpersonatorUserId = "staff-1",
    };

    private static (AuditTrailCreateContext Context, AuditTrailEvent Event) ContentEventAsRecordedByStock()
    {
        // What the stock content audit handler produces under impersonation: the request
        // principal is the member's.
        var context = new AuditTrailCreateContext<object>("Created", "Content", "item-1", "member-1", "mia", new object());
        var auditTrailEvent = new AuditTrailEvent { EventId = "event-1", Category = "Content", Name = "Created", CorrelationId = "item-1", UserId = "member-1", UserName = "mia" };
        auditTrailEvent.Put(new AuditTrailAttribution());
        return (context, auditTrailEvent);
    }

    [Fact]
    public async Task ImpersonatedActionNamesTheStaffUserTheMemberAndTheOrganization()
    {
        var handler = new ImpersonationAuditTrailEventHandler(new HttpContextAccessor(), new Callers(ImpersonatedMember()));
        var (context, auditTrailEvent) = ContentEventAsRecordedByStock();

        await handler.AlterAsync(context, auditTrailEvent);

        var attribution = auditTrailEvent.GetOrCreate<AuditTrailAttribution>();
        Assert.Equal("staff-1", auditTrailEvent.UserId);
        Assert.Equal("member-1", attribution.ImpersonatedUserId);
        Assert.Equal("mia", attribution.ImpersonatedUserName);
        Assert.Equal("member-1", attribution.FunctionalUserId);
        Assert.Equal("org-1", attribution.OrganizationId);
        Assert.True(attribution.IsImpersonated);
    }

    [Fact]
    public async Task UnimpersonatedActionIsLeftAlone()
    {
        var caller = ImpersonatedMember();
        var member = new CallerContext { Tenant = caller.Tenant, Side = caller.Side, UserId = caller.UserId, UserName = caller.UserName, OrganizationId = caller.OrganizationId };
        var handler = new ImpersonationAuditTrailEventHandler(new HttpContextAccessor(), new Callers(member));
        var (context, auditTrailEvent) = ContentEventAsRecordedByStock();

        await handler.AlterAsync(context, auditTrailEvent);

        Assert.Equal("member-1", auditTrailEvent.UserId);
        Assert.Null(auditTrailEvent.GetOrCreate<AuditTrailAttribution>().ImpersonatedUserId);
    }

    [Fact]
    public async Task AnEventAttributedToSomeoneElseKeepsItsUser()
    {
        var handler = new ImpersonationAuditTrailEventHandler(new HttpContextAccessor(), new Callers(ImpersonatedMember()));
        var (context, auditTrailEvent) = ContentEventAsRecordedByStock();
        auditTrailEvent.UserId = "someone-else";

        await handler.AlterAsync(context, auditTrailEvent);

        Assert.Equal("someone-else", auditTrailEvent.UserId);
    }

    [Theory]
    [InlineData("ViewAuditTrail")]
    [InlineData("ManageAuditTrailSettings")]
    public void MembersNeverHoldTheAuditPermissions(string permission)
    {
        // The ceiling Members registers (Startup): members never see the trail (docs/audit.md › step 4).
        var options = new MemberPermissionCeilingOptions().Ceiling("ViewAuditTrail", "ManageAuditTrailSettings");
        var ceiling = new MemberClassCeiling(Options.Create(options));
        var member = new CallerContext { Tenant = "Default", Side = CallerSide.Member, UserId = "member-1", UserClass = "member" };

        Assert.NotNull(ceiling.Deny(member, permission));
    }
}
