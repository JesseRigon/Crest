using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// Displays a reference line at a fixed value over a chart series. Use it to highlight targets, thresholds or limits on the value axis.
    /// </summary>
    /// <example>
    /// <code>
    ///   &lt;GroveChart&gt;
    ///       &lt;GroveLineSeries Data=@revenue CategoryProperty="Quarter" ValueProperty="Revenue"&gt;
    ///          &lt;GroveSeriesReferenceLine Value="280000" Title="Target" /&gt;
    ///       &lt;/GroveLineSeries&gt;
    ///   &lt;/GroveChart&gt;
    ///   @code {
    ///       class DataItem
    ///       {
    ///           public string Quarter { get; set; }
    ///           public double Revenue { get; set; }
    ///       }
    ///       DataItem[] revenue = new DataItem[]
    ///       {
    ///           new DataItem { Quarter = "Q1", Revenue = 234000 },
    ///           new DataItem { Quarter = "Q2", Revenue = 284000 },
    ///           new DataItem { Quarter = "Q3", Revenue = 274000 },
    ///           new DataItem { Quarter = "Q4", Revenue = 294000 }
    ///       };
    ///   }
    /// </code>
    /// </example>
    public partial class GroveSeriesReferenceLine : GroveSeriesValueLine
    {
        /// <summary>
        /// Specifies the title of the reference line. Displayed as label in the tooltip. Set to <c>"Reference"</c> by default.
        /// </summary>
        [Parameter]
        public string? Title { get; set; }

        /// <inheritdoc />
        protected override string Name => Title ?? "Reference";

        /// <inheritdoc />
        protected override bool ShouldRefreshChart(ParameterView parameters)
        {
            return DidParameterChange(parameters, nameof(Value), Value) || base.ShouldRefreshChart(parameters);
        }
    }
}
