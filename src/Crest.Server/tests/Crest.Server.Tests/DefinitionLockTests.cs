using Crest.ContentTypes;
using Crest.Services;
using Crest.Settings;
using NSubstitute;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Builders;
using Crest.ContentManagement.Metadata.Models;
using Xunit;

namespace Crest.Server.Tests;

// Definition locks (docs/content-items.md › Definition locks): the source ranking, the lift
// rules, what the builders store, and the guard's refusals over stored definitions.
public class DefinitionLockTests
{
    private static ContentPartDefinition Part(string partLock = DefinitionLockSources.None, string fieldLock = DefinitionLockSources.None)
    {
        var builder = new ContentPartDefinitionBuilder().Named("InvoicePart")
            .WithField("Total", field => field.OfType("TextField").Locked(fieldLock))
            .WithField("Notes", field => field.OfType("TextField"));
        if (partLock != DefinitionLockSources.None) builder.Locked(partLock);
        return builder.Build();
    }

    private static ContentTypeDefinition Type(string attachmentLock)
    {
        var builder = new ContentTypeDefinitionBuilder().WithName("Invoice")
            .WithPart("InvoicePart", part => { if (attachmentLock != DefinitionLockSources.None) part.Locked(attachmentLock); })
            .WithPart("TitlePart", part => { });
        return builder.Build();
    }

    private static DefinitionLockGuard Guard(ContentPartDefinition? part = null, ContentTypeDefinition? type = null)
    {
        var manager = Substitute.For<IContentDefinitionManager>();
        manager.LoadPartDefinitionAsync(Arg.Any<string>()).Returns(part);
        manager.LoadTypeDefinitionAsync(Arg.Any<string>()).Returns(type);
        return new DefinitionLockGuard(manager);
    }

    [Fact]
    public void Sources_normalize_and_rank()
    {
        Assert.Equal(DefinitionLockSources.Module, DefinitionLockSources.Normalize(" module "));
        Assert.Equal(DefinitionLockSources.None, DefinitionLockSources.Normalize("bogus"));
        Assert.Equal(DefinitionLockSources.None, DefinitionLockSources.Normalize(null));
        Assert.Equal(DefinitionLockSources.Module, DefinitionLockRules.Strongest(DefinitionLockSources.Tenant, DefinitionLockSources.Module));
        Assert.Equal(DefinitionLockSources.Tenant, DefinitionLockRules.Strongest(DefinitionLockSources.None, DefinitionLockSources.Tenant));
    }

    [Fact]
    public void Tenants_move_locks_between_none_and_tenant_only()
    {
        Assert.True(DefinitionLockRules.CanTenantChange(DefinitionLockSources.None, DefinitionLockSources.Tenant));
        Assert.True(DefinitionLockRules.CanTenantChange(DefinitionLockSources.Tenant, DefinitionLockSources.None));
        Assert.False(DefinitionLockRules.CanTenantChange(DefinitionLockSources.Module, DefinitionLockSources.None));
        Assert.False(DefinitionLockRules.CanTenantChange(DefinitionLockSources.None, DefinitionLockSources.Module));
        Assert.Throws<DefinitionLockedException>(() => DefinitionLockGuard.EnsureLockChange(DefinitionLockSources.Module, DefinitionLockSources.None, "Field 'x'"));
    }

    [Fact]
    public void Builders_store_the_lock_and_fields_inherit_the_parts()
    {
        var part = Part(fieldLock: DefinitionLockSources.Module);
        Assert.Equal(DefinitionLockSources.Module, part.Fields.Single(f => f.Name == "Total").GetLock());
        Assert.Equal(DefinitionLockSources.None, part.Fields.Single(f => f.Name == "Notes").GetEffectiveLock(part));

        var lockedPart = Part(partLock: DefinitionLockSources.Tenant);
        Assert.Equal(DefinitionLockSources.Tenant, lockedPart.GetLock());
        Assert.Equal(DefinitionLockSources.Tenant, lockedPart.Fields.Single(f => f.Name == "Notes").GetEffectiveLock(lockedPart));

        Assert.Equal(DefinitionLockSources.Module, Type(DefinitionLockSources.Module).Parts.Single(p => p.Name == "InvoicePart").GetLock());
    }

    [Fact]
    public async Task Guard_refuses_changes_to_locked_fields_and_allows_the_rest()
    {
        var guard = Guard(Part(fieldLock: DefinitionLockSources.Module));

        var refused = await Assert.ThrowsAsync<DefinitionLockedException>(() => guard.EnsureFieldChangeAsync("InvoicePart", "Total", DefinitionChanges.Retyped));
        Assert.Contains("InvoicePart.Total", refused.Message);
        Assert.Contains("Module", refused.Message);

        await guard.EnsureFieldChangeAsync("InvoicePart", "Notes", DefinitionChanges.Removed);
        await guard.EnsureFieldChangeAsync("InvoicePart", "Missing", DefinitionChanges.Removed);
        await guard.EnsurePartChangeAsync("InvoicePart", DefinitionChanges.Deleted);
    }

    [Fact]
    public async Task A_part_lock_covers_every_field_and_the_part_itself()
    {
        var guard = Guard(Part(partLock: DefinitionLockSources.Tenant));

        await Assert.ThrowsAsync<DefinitionLockedException>(() => guard.EnsureFieldChangeAsync("InvoicePart", "Notes", DefinitionChanges.SettingsChanged));
        await Assert.ThrowsAsync<DefinitionLockedException>(() => guard.EnsurePartChangeAsync("InvoicePart", DefinitionChanges.Deleted));
    }

    [Fact]
    public async Task A_locked_attachment_keeps_the_part_on_the_type_and_the_type_alive()
    {
        var guard = Guard(type: Type(DefinitionLockSources.Module));

        await Assert.ThrowsAsync<DefinitionLockedException>(() => guard.EnsureTypePartChangeAsync("Invoice", "InvoicePart", DefinitionChanges.Detached));
        await guard.EnsureTypePartChangeAsync("Invoice", "TitlePart", DefinitionChanges.Detached);
        await Assert.ThrowsAsync<DefinitionLockedException>(() => guard.EnsureTypeDeleteAsync("Invoice"));

        var open = Guard(type: Type(DefinitionLockSources.None));
        await open.EnsureTypeDeleteAsync("Invoice");
        await open.EnsureTypeDeleteAsync("Unknown");
    }
}
