using System.Threading.Tasks;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// Non-generic contract for <see cref="GroveSpiderChart"/> used by configuration components
    /// like <see cref="GroveSpiderLegend"/> without relying on reflection (important for trimming/AOT).
    /// </summary>
    public interface IGroveSpiderChart
    {
        /// <summary>
        /// Gets or sets the legend configuration for the chart.
        /// </summary>
        GroveSpiderLegend Legend { get; set; }

        /// <summary>
        /// Requests the chart to refresh its rendering.
        /// </summary>
        Task Refresh();
    }
}

