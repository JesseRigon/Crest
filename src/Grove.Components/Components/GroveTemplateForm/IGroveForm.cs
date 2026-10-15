using Grove.Components.Primitives;

namespace Grove.Components.Primitives;

/// <summary>
/// Represents the common <see cref="GroveTemplateForm{TItem}" /> API used by
/// its items. Injected as a cascading property in <see cref="IGroveFormComponent" />.
/// </summary>
public interface IGroveForm
{
    /// <summary>
    /// Adds the specified component to the form.
    /// </summary>
    /// <param name="component">The component to add to the form.</param>
    void AddComponent(IGroveFormComponent component);

    /// <summary>
    /// Removes the component from the form.
    /// </summary>
    /// <param name="component">The component to remove from the form.</param>
    void RemoveComponent(IGroveFormComponent component);

    /// <summary>
    /// Finds a form component by its name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The component whose <see cref="IGroveFormComponent.Name" /> equals to <paramref name="name" />; <c>null</c> if such a component is not found.</returns>
    IGroveFormComponent FindComponent(string name);
}

