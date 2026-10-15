using Grove.AuditTrail.Services;
using Grove.AuditTrail.Services.Models;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Models;
using Grove.Modules;
using Grove.Infrastructure.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Grove.Tests.AuditTrail;

/// <summary>
/// The trail starts once the tenant is set up (docs/audit.md): setup migrates every feature in
/// one pass, so an event recorded by a feature migrated before AuditTrail would be flushed into
/// audit tables that do not exist yet and roll back the whole setup.
/// </summary>
public class AuditTrailManagerSetupTests
{
    private sealed class Event;

    [Fact]
    public async Task WhileInitializing_NothingIsRecordedOrRead()
    {
        // Strict: any call on the store or the settings fails the test.
        var session = new Mock<YesSql.ISession>(MockBehavior.Strict);
        var siteService = new Mock<ISiteService>(MockBehavior.Strict);

        var manager = new AuditTrailManager(
            Mock.Of<IClock>(),
            session.Object,
            siteService.Object,
            Options.Create(new AuditTrailOptions()),
            Mock.Of<ILookupNormalizer>(),
            [],
            Mock.Of<IClientIPAddressAccessor>(),
            Mock.Of<IAuditTrailIdGenerator>(),
            new ShellSettings { State = TenantState.Initializing },
            NullLogger<AuditTrailManager>.Instance);

        await manager.RecordEventAsync(new AuditTrailContext<Event>("Created", "Content", "id", "user", "admin", new Event()));

        session.VerifyNoOtherCalls();
        siteService.VerifyNoOtherCalls();
    }
}
