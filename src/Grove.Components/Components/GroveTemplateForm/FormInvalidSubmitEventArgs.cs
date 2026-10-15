using System.Collections.Generic;

namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Grove.Components.Primitives.GroveTemplateForm{TItem}.InvalidSubmit" /> event that is being raised.
/// </summary>
public class FormInvalidSubmitEventArgs
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IEnumerable<string>? Errors { get; set; }
}

