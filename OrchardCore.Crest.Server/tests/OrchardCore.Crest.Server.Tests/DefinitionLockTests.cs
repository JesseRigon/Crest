using Crest.Services;
using Crest.Settings;
using NSubstitute;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using Xunit;

namespace OrchardCore.Crest.Server.Tests;

// Definition locks (docs/content-items.md › Definition locks): the source ranking, the lift
// rules, what the builders store, and the guard's refusals over stored definitions.
public class DefinitionLockTests
{
    private static ContentPartDefinition Part(string partLock = CrestDefinitionLockSources.None, string fieldLock = CrestDefinitionLockSources.None)
    {
        var builder = new ContentPartDefinitionBuilder().Named("InvoicePart")
            .WithField("Total", field => field.OfType("TextField").Locked(fieldLock))
            .WithField("Notes", field => field.OfType("TextField"));
        if (partLock != CrestDefinitionLockSources.None) builder.Locked(partLock);
        return builder.Build();
    }

    private static ContentTypeDefinition Type(string attachmentLock)
    {
        var builder = new ContentTypeDefinitionBuilder().WithName("Invoice")
            .WithPart("InvoicePart", part => { if (attachmentLock != CrestDefinitionLockSources.None) part.Locked(attachmentLock); })
            .WithPart("TitlePart", part => { });
        return builder.Build();
    }

    private static CrestDefinitionLockGuard Guard(ContentPartDefinition? part = null, ContentTypeDefinition? type = null)
    {
        var manager = Substitute.For<IContentDefinitionManager>();
        manager.LoadPartDefinitionAsync(Arg.Any<string>()).Returns(part);
        manager.LoadTypeDefinitionAsync(Arg.Any<string>()).Returns(type);
        return new CrestDefinitionLockGuard(manager);
    }

    [Fact]
    public void Sources_normalize_and_rank()
    {
        Assert.Equal(CrestDefinitionLockSources.Module, CrestDefinitionLockSources.Normalize(" module "));
        Assert.Equal(CrestDefinitionLockSources.None, CrestDefinitionLockSources.Normalize("bogus"));
        Assert.Equal(CrestDefinitionLockSources.None, CrestDefinitionLockSources.Normalize(null));
        Assert.Equal(CrestDefinitionLockSources.Module, CrestDefinitionLockRules.Strongest(CrestDefinitionLockSources.Tenant, CrestDefinitionLockSources.Module));
        Assert.Equal(CrestDefinitionLockSources.Tenant, CrestDefinitionLockRules.Strongest(CrestDefinitionLockSources.None, CrestDefinitionLockSources.Tenant));
    }

    [Fact]
    public void Tenants_move_locks_between_none_and_tenant_only()
    {
        Assert.True(CrestDefinitionLockRules.CanTenantChange(CrestDefinitionLockSources.None, CrestDefinitionLockSources.Tenant));
        Assert.True(CrestDefinitionLockRules.CanTenantChange(CrestDefinitionLockSources.Tenant, CrestDefinitionLockSources.None));
        Assert.False(CrestDefinitionLockRules.CanTenantChange(CrestDefinitionLockSources.Module, CrestDefinitionLockSources.None));
        Assert.False(CrestDefinitionLockRules.CanTenantChange(CrestDefinitionLockSources.None, CrestDefinitionLockSources.Module));
        Assert.Throws<CrestDefinitionLockedException>(() => CrestDefinitionLockGuard.EnsureLockChange(CrestDefinitionLockSources.Module, CrestDefinitionLockSources.None, "Field 'x'"));
    }

    [Fact]
    public void Builders_store_the_lock_and_fields_inherit_the_parts()
    {
        var part = Part(fieldLock: CrestDefinitionLockSources.Module);
        Assert.Equal(CrestDefinitionLockSources.Module, part.Fields.Single(f => f.Name == "Total").GetLock());
        Assert.Equal(CrestDefinitionLockSources.None, part.Fields.Single(f => f.Name == "Notes").GetEffectiveLock(part));

        var lockedPart = Part(partLock: CrestDefinitionLockSources.Tenant);
        Assert.Equal(CrestDefinitionLockSources.Tenant, lockedPart.GetLock());
        Assert.Equal(CrestDefinitionLockSources.Tenant, lockedPart.Fields.Single(f => f.Name == "Notes").GetEffectiveLock(lockedPart));

        Assert.Equal(CrestDefinitionLockSources.Module, Type(CrestDefinitionLockSources.Module).Parts.Single(p => p.Name == "InvoicePart").GetLock());
    }

    [Fact]
    public async Task Guard_refuses_changes_to_locked_fields_and_allows_the_rest()
    {
        var guard = Guard(Part(fieldLock: CrestDefinitionLockSources.Module));

        var refused = await Assert.ThrowsAsync<CrestDefinitionLockedException>(() => guard.EnsureFieldChangeAsync("InvoicePart", "Total", CrestDefinitionChanges.Retyped));
        Assert.Contains("InvoicePart.Total", refused.Message);
        Assert.Contains("Module", refused.Message);

        await guard.EnsureFieldChangeAsync("InvoicePart", "Notes", CrestDefinitionChanges.Removed);
        await guard.EnsureFieldChangeAsync("InvoicePart", "Missing", CrestDefinitionChanges.Removed);
        await guard.EnsurePartChangeAsync("InvoicePart", CrestDefinitionChanges.Deleted);
    }

    [Fact]
    public async Task A_part_lock_covers_every_field_and_the_part_itself()
    {
        var guard = Guard(Part(partLock: CrestDefinitionLockSources.Tenant));

        await Assert.ThrowsAsync<CrestDefinitionLockedException>(() => guard.EnsureFieldChangeAsync("InvoicePart", "Notes", CrestDefinitionChanges.SettingsChanged));
        await Assert.ThrowsAsync<CrestDefinitionLockedException>(() => guard.EnsurePartChangeAsync("InvoicePart", CrestDefinitionChanges.Deleted));
    }

    [Fact]
    public async Task A_locked_attachment_keeps_the_part_on_the_type_and_the_type_alive()
    {
        var guard = Guard(type: Type(CrestDefinitionLockSources.Module));

        await Assert.ThrowsAsync<CrestDefinitionLockedException>(() => guard.EnsureTypePartChangeAsync("Invoice", "InvoicePart", CrestDefinitionChanges.Detached));
        await guard.EnsureTypePartChangeAsync("Invoice", "TitlePart", CrestDefinitionChanges.Detached);
        await Assert.ThrowsAsync<CrestDefinitionLockedException>(() => guard.EnsureTypeDeleteAsync("Invoice"));

        var open = Guard(type: Type(CrestDefinitionLockSources.None));
        await open.EnsureTypeDeleteAsync("Invoice");
        await open.EnsureTypeDeleteAsync("Unknown");
    }
}
