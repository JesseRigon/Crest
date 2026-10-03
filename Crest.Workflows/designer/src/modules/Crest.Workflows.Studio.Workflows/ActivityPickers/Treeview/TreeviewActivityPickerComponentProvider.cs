using Crest.Workflows.Studio.Contracts;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.Workflows.ActivityPickers.Treeview;

/// <summary>
/// Represents a provider for a Treeview-based activity picker component.
/// </summary>
public class TreeviewActivityPickerComponentProvider : IActivityPickerComponentProvider
{
    /// <inheritdoc />
    public RenderFragment GetActivityPickerComponent() => builder =>
    {
        builder.OpenComponent<ActivityPicker>(0);
        builder.CloseComponent();
    };
}