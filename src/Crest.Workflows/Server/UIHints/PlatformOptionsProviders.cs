using System.Reflection;
using Crest.Workflows.Registry;
using Crest.Workflows.UIHints.Dropdown;
using Crest.Security.Permissions;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Services;

namespace Crest.Workflows.UIHints;

/// <summary>The tenant's Crest permissions (every enabled module's), for permission inputs.</summary>
public sealed class PermissionOptionsProvider(IPermissionService permissionService) : DropDownOptionsProviderBase
{
    protected override async ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken)
    {
        var permissions = await permissionService.GetPermissionsAsync();
        return [new SelectListItem(string.Empty, string.Empty), .. permissions
            .OrderBy(p => p.Category ?? string.Empty).ThenBy(p => p.Name)
            .Select(p => new SelectListItem(string.IsNullOrWhiteSpace(p.Description) ? p.Name : $"{p.Description} ({p.Name})", p.Name))];
    }
}

/// <summary>The triggers the enabled modules registered.</summary>
public sealed class WorkflowTriggerOptionsProvider(WorkflowRegistryCatalog catalog) : DropDownOptionsProviderBase
{
    protected override ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken) =>
        new(catalog.Triggers.Select(t => new SelectListItem($"{t.DisplayName} ({t.Key})", t.Key)).ToList());
}

/// <summary>The hook slots the enabled modules registered.</summary>
public sealed class HookSlotOptionsProvider(WorkflowRegistryCatalog catalog) : DropDownOptionsProviderBase
{
    protected override ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken) =>
        new(catalog.HookSlots.Select(s => new SelectListItem($"{s.DisplayName} ({s.Key})", s.Key)).ToList());
}

/// <summary>Stock Crest events (<see cref="StockEventOptionsProvider"/>) or tasks the enabled modules registered, less the engine-native ones.</summary>
public abstract class StockActivityOptionsProvider(IActivityLibrary activityLibrary) : DropDownOptionsProviderBase
{
    protected abstract bool Events { get; }

    protected override ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken) =>
        new(activityLibrary.ListActivities()
            .Where(a => a is IEvent == Events && !Platform.StockActivityRunner.EngineNative.Contains(a.Name))
            .OrderBy(a => a.Category.Value).ThenBy(a => a.DisplayText.Value)
            .Select(a => new SelectListItem($"{a.Category.Value}: {a.DisplayText.Value}", a.Name))
            .ToList());
}

public sealed class StockEventOptionsProvider(IActivityLibrary activityLibrary) : StockActivityOptionsProvider(activityLibrary)
{
    protected override bool Events => true;
}

public sealed class StockTaskOptionsProvider(IActivityLibrary activityLibrary) : StockActivityOptionsProvider(activityLibrary)
{
    protected override bool Events => false;
}
