using System;
using System.Collections.Generic;
using Grove.Components.Primitives;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// Supplies information about a <see cref="GroveScheduler{TItem}.DaySelect" /> event that is being raised.
    /// </summary>
    public class SchedulerDaySelectEventArgs
    {
        /// <summary>
        /// Selected date.
        /// </summary>
        public DateTime Day { get; set; }
        /// <summary>
        /// List of appointments.
        /// </summary>
        public IEnumerable<AppointmentData>? Appointments { get; set; }
        /// <summary>
        /// Current View.
        /// </summary>
        public ISchedulerView? View { get; set; }
    }
}