namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// Specifies the visual style of a <see cref="GroveLinearGaugeScalePointer" />.
    /// </summary>
    public enum LinearGaugePointerType
    {
        /// <summary>
        /// A triangular arrow that points to the current value on the scale line.
        /// </summary>
        Arrow,
        /// <summary>
        /// A filled rectangle that extends from the scale minimum to the current value.
        /// </summary>
        Bar,
        /// <summary>
        /// A thin line perpendicular to the scale axis at the current value position.
        /// </summary>
        Line
    }
}
