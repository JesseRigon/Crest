#nullable enable

namespace Grove.DisplayManagement.Components;

/// <summary>What a template binding is asked for one shape (§ 2): the shape, the instance id the
/// builder gave it, and the names it resolves by, most specific (last) alternate first.</summary>
public sealed record DisplayTemplateRequest(
    IShape Shape,
    string InstanceId,
    string ShapeType,
    IReadOnlyList<string> Alternates,
    string? DisplayType);

/// <summary>
/// A template binding (§ 2): asked before the code bindings, so a tenant template overrides a
/// module's component for that shape. Answers the template's evaluated tree with
/// <see cref="DisplayNode.ShapeZoneComponent"/> placeholders where the shape's own zones go,
/// or null when no template binds the shape. The Templates module implements it.
/// </summary>
public interface IDisplayTemplateBinding
{
    Task<DisplayNode?> BindAsync(DisplayTemplateRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// The instance overrides of § 5a, applied to every tree the builder finishes: values a tenant set
/// for one placement, over what it inherits. The Templates module implements it.
/// </summary>
public interface IDisplayTreeOverrides
{
    Task ApplyAsync(DisplayNode root, CancellationToken cancellationToken = default);
}
