using System.Collections.Generic;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// Marker interface for <see cref="GroveFullStackedAreaSeries{TItem}" />.
    /// </summary>
    public interface IChartFullStackedAreaSeries
    {
        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>The count.</value>
        int Count { get; }

        /// <summary>
        /// Gets the values for category.
        /// </summary>
        IEnumerable<double> ValuesForCategory(double category);

        /// <summary>
        /// Gets the value at the specified index.
        /// </summary>
        double ValueAt(int index);
    }
}
