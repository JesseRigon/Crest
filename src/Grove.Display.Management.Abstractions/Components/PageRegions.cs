namespace Grove.Display.Components.Regions;

/// <summary>
/// The UI-injection seam (docs/page-regions.md), now the code form of a zone contribution
/// (decision 6): a page declares a region with <c>GrovePageRegion</c> under an open zone key; a
/// DOWNSTREAM module contributes a component to that key without the page's module referencing
/// it. The component registry collects contributors from the module libraries with their
/// components (<see cref="Grove.Display.Management.Components.ZoneContribution"/>), so a disabled
/// feature's contribution is absent. Nothing registered renders nothing. Keys belong to the
/// module owning the page, declared with <see cref="Grove.Display.Management.Components.ZoneKeyAttribute"/>.
/// </summary>
public interface IPageRegionContributor
{
    string RegionKey { get; }

    /// <summary>The default priority: lower renders first.</summary>
    int Order { get; }

    /// <summary>The component to render; it receives the region's context as the parameter named <c>Context</c>.</summary>
    Type ComponentType { get; }
}
