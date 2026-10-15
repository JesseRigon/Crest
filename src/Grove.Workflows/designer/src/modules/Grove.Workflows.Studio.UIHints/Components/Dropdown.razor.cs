using Grove.Workflows.Api.Client.Shared.UIHints.DropDown;
using Grove.Workflows.Studio.Models;
using Grove.Workflows.Studio.UIHints.Extensions;
using Microsoft.AspNetCore.Components;

namespace Grove.Workflows.Studio.UIHints.Components;

/// <summary>
/// A component that renders a dropdown.
/// </summary>
public partial class Dropdown
{
    private ICollection<SelectListItem> _items = [];

    /// <summary>
    /// The editor context.
    /// </summary>
    [Parameter] public DisplayInputEditorContext EditorContext { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        var selectList = EditorContext.InputDescriptor.GetSelectList();
        _items = selectList.Items.OrderBy(x => x.Text).ToList();
    }

    private SelectListItem? GetSelectedValue()
    {
        var value = EditorContext.GetLiteralValueOrDefault();
        return _items.FirstOrDefault(x => x.Value == value);
    }
    
    private async Task OnValueChanged(SelectListItem? value)
    {
        await EditorContext.UpdateValueOrLiteralExpressionAsync(value?.Value ?? "");
    }
}