using Grove.DisplayManagement;
using Grove.DisplayManagement.Components;
using Grove.DisplayManagement.Descriptors;

namespace Grove.Services;

// Participates in Grove's own shape pipeline (DefaultHtmlDisplay.GetShapeBindingAsync
// tries every registered IShapeBindingResolver before the compiled ShapeTable) exactly
// the way Grove.Templates' TemplatesShapeBindingResolver overrides a shape's
// rendering with tenant-stored Liquid. Falls through (returns null) for every shape that is
// not a component in the request's component registry (docs/blazor-display.md § 1: per
// tenant, theme and shell, only the enabled features' components), so normal
// Liquid/Razor/Templates-resolved shapes are unaffected.
//
// A bound shape renders through the display tree (ComponentShapeRenderer): its properties are
// read by the component's schema and converted to the parameters' types, its zones become
// child nodes, and the whole tree renders in one pass with HtmlRenderer. Static SSR only: no
// hydration, no @rendermode. Retired with the Liquid views (rework plan step 7).
public sealed class BlazorComponentShapeBindingResolver : IShapeBindingResolver
{
    private readonly IComponentRegistry _registry;

    public BlazorComponentShapeBindingResolver(IComponentRegistry registry)
    {
        _registry = registry;
    }

    public Task<ShapeBinding> GetShapeBindingAsync(string shapeType)
    {
        if (!_registry.TryGet(shapeType, out var descriptor) || descriptor.Type is null)
        {
            return Task.FromResult<ShapeBinding>(null);
        }

        return Task.FromResult(ShapeBinding.ForComponent(shapeType, descriptor.Name));
    }
}
