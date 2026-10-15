using System.Collections.Generic;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// Marker interface for <see cref="GroveStackedAreaSeries{TItem}" />.
    /// </summary>
    public interface IChartStackedAreaSeries
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