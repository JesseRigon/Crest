namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// Specifies the interpolation mode of lines between data points. Used by <see cref="GroveAreaSeries{TItem}"/> and <see cref="GroveLineSeries{TItem}"/>.
    /// </summary>
    public enum Interpolation
    {

        /// <summary>
        /// Points are connected by a straight line.
        /// </summary>
        Line,
        /// <summary>
        /// Points are connected by a smooth curve.
        /// </summary>
        Spline,
        /// <summary>
        /// Points are connected by horizontal and vertical lines only.
        /// </summary>
        Step
    }
}
