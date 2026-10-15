namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// Common axis API of <see cref="GroveChart" />
    /// </summary>
    public interface IChartAxis
    {
        /// <summary>
        /// Gets or sets the grid lines configuration of this axis.
        /// </summary>
        GroveGridLines GridLines { get; set; }

        /// <summary>
        /// Gets or sets the crosshair configuration of this axis.
        /// </summary>
        GroveAxisCrosshair Crosshair { get; set; }
    }
}