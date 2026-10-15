namespace Grove.Display.Components.Primitives;

/// <summary>
/// Specifies the ways a <see cref="Grove.Display.Components.Primitives.GroveAccordion" /> component renders its items.
/// </summary>
public enum AccordionRenderMode
{
    /// <summary>
    /// The GroveAccordion component switches its items server side. The component re-renders on every expand/collapse.
    /// </summary>
    Server,

    /// <summary>
    /// The GroveAccordion component switches its items client-side. All items are rendered and the expand/collapse is handled with JavaScript.
    /// </summary>
    Client
}
