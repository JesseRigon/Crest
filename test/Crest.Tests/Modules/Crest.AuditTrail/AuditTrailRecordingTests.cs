#nullable enable

using System.Text.Json.Nodes;
using Crest.Access;
using Crest.Access.AuditTrail;
using Crest.AuditTrail.Models;
using Crest.AuditTrail.Recorders;
using Crest.AuditTrail.Services.Models;
using Crest.Entities;

namespace Crest.Tests.Modules.Crest.AuditTrail;

public class AuditingSiteServiceTests
{
    [Fact]
    public void OnlyTheNamesOfChangedSectionsAreReported()
    {
        var before = JsonNode.Parse("""{"SiteName":"A","Properties":{"SmtpSettings":{"Password":"x"},"CurrentThemeName":"One","Untouched":{"V":1}}}""")!.AsObject();
        var after = JsonNode.Parse("""{"SiteName":"A","Properties":{"SmtpSettings":{"Password":"y"},"CurrentThemeName":"Two","Untouched":{"V":1},"Added":{}}}""")!.AsObject();

        var sections = AuditingSiteService.ChangedSections(before, after).ToArray();

        Assert.Equal(["Added", "CurrentThemeName", "SmtpSettings"], sections);
    }

    [Fact]
    public void ATopLevelChangeIsGeneral()
    {
        var before = JsonNode.Parse("""{"SiteName":"A","Properties":{}}""")!.AsObject();
        var after = JsonNode.Parse("""{"SiteName":"B","Properties":{}}""")!.AsObject();

        Assert.Equal(["General"], AuditingSiteService.ChangedSections(before, after));
    }
}

/// <summary>docs/audit.md › Machine actors: an API client acting without a user is recorded with its client id.</summary>
public class CallerAuditTrailEventHandlerTests
{
    private sealed class Callers(CallerContext? current) : ICallerContextAccessor
    {
        public CallerContext? Current { get; set; } = current;
    }

    private static (AuditTrailCreateContext, AuditTrailEvent) Event(string userId)
    {
        var context = new AuditTrailCreateContext<object>("Updated", "Settings", "Site", userId, null, new object());
        var auditTrailEvent = new AuditTrailEvent { EventId = "e", Category = "Settings", Name = "Updated", UserId = userId };
        auditTrailEvent.Put(new AuditTrailAttribution());
        return (context, auditTrailEvent);
    }

    [Fact]
    public async Task AMachineIsRecordedWithItsClientId()
    {
        var caller = new CallerContext { Tenant = "Default", Side = CallerSide.Admin, UserId = "ledger-sync", UserName = "Ledger sync", UserClass = CallerClasses.Application };
        var (context, auditTrailEvent) = Event(string.Empty);

        await new CallerAuditTrailEventHandler(new Callers(caller)).AlterAsync(context, auditTrailEvent);

        var attribution = auditTrailEvent.As<AuditTrailAttribution>();
        Assert.Equal("ledger-sync", auditTrailEvent.UserId);
        Assert.Equal(AuditTrailActorKinds.Application, attribution.ActorKind);
        Assert.Equal("ledger-sync", attribution.ClientId);
        Assert.Equal("Admin", attribution.Side);
    }

    [Fact]
    public async Task TheSideAndOrganizationComeFromTheCaller()
    {
        var caller = new CallerContext { Tenant = "Default", Side = CallerSide.Member, UserId = "member-1", OrganizationId = "org-1" };
        var (context, auditTrailEvent) = Event("member-1");

        await new CallerAuditTrailEventHandler(new Callers(caller)).AlterAsync(context, auditTrailEvent);

        var attribution = auditTrailEvent.As<AuditTrailAttribution>();
        Assert.Equal(AuditTrailActorKinds.User, attribution.ActorKind);
        Assert.Equal("Member", attribution.Side);
        Assert.Equal("org-1", attribution.OrganizationId);
    }
}
