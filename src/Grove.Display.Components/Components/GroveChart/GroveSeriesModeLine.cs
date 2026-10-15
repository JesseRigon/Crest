using System;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// Displays the mode of a chart series.
    /// </summary>
    /// <example>
    /// <code>
    ///   &lt;GroveChart&gt;
    ///       &lt;GroveLineSeries Data=@revenue CategoryProperty="Quarter" ValueProperty="Revenue"&gt;
    ///          &lt;GroveSeriesModeLine /&gt;
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
    public partial class GroveSeriesModeLine : GroveSeriesValueLine
    {
        /// <ihnheritdoc />
        public override double Value
        {
            get
            {
                return Series?.GetMode() ?? 0;
            }
            set
            {
                throw new InvalidOperationException("Value is computed and cannot be set");
            }
        }

        /// <ihnheritdoc />
        protected override string Name => "Mode";
    }
}
