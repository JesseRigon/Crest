using System;
using Grove.Components.Primitives;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// Supplies information about a <see cref="GroveScheduler{TItem}.LoadData" /> event that is being raised.
    /// </summary>
    public class SchedulerLoadDataEventArgs
    {
        /// <summary>
        /// The start of the currently rendered period.
        /// </summary>
        public DateTime Start { get; set; }
        /// <summary>
        /// The start of the currently rendered period.
        /// </summary>
        public DateTime End { get; set; }
        /// <summary>
        /// The selected view of the scheduler.
        /// </summary>
        public ISchedulerView? View { get; set; }

    }
}