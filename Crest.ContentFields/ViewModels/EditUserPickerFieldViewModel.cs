using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentFields.ViewModels;

public class EditUserPickerFieldViewModel
{
    public string UserIds { get; set; }

    [BindNever]
    public UserPickerField Field { get; set; }

    [BindNever]
    public ContentPart Part { get; set; }

    [BindNever]
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }

    [BindNever]
    public ContentTypePartDefinition TypePartDefinition { get; set; }

    [BindNever]
    public IList<VueMultiselectUserViewModel> SelectedUsers { get; set; } = [];
}

public class VueMultiselectUserViewModel
{
    public string Id { get; set; }
    public string DisplayText { get; set; }
    public bool IsEnabled { get; set; }
}
