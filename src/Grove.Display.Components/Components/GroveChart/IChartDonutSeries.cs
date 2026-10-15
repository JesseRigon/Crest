using Microsoft.AspNetCore.Components;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// Marker interface for <see cref="GroveColumnSeries{TItem}" />.
    /// </summary>
    public interface IChartDonutSeries
    {
        /// <summary>
        /// Renders the title.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>RenderFragment.</returns>
        RenderFragment RenderTitle(double x, double y);
    }
}