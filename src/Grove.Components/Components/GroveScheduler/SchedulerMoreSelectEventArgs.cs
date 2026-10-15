using System;
using System.Collections.Generic;
using Grove.Components.Primitives;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// Supplies information about a <see cref="GroveScheduler{TItem}.MoreSelect" /> event that is being raised.
    /// </summary>
    public class SchedulerMoreSelectEventArgs
    {
        /// <summary>
        /// The start of the slot.
        /// </summary>
        public DateTime Start { get; set; }
        /// <summary>
        /// The end of the slot.
        /// </summary>
        public DateTime End { get; set; }
        /// <summary>
        /// List of appointments.
        /// </summary>
        public IEnumerable<AppointmentData>? Appointments { get; set; }
        /// <summary>
        /// Current View.
        /// </summary>
        public ISchedulerView? View { get; set; }
        /// <summary>
        /// Has default action been prevented from occuring.
        /// </summary>
        public bool IsDefaultPrevented { get; private set; }
        /// <summary>
        /// Prevent the default action from occuring.
        /// </summary>
        public void PreventDefault()
        {
            IsDefaultPrevented = true;
        }
    }
}