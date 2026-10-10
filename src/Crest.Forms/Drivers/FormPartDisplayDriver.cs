using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;
using Crest.Forms.Models;
using Crest.Forms.ViewModels;

namespace Crest.Forms.Drivers;

public sealed class FormPartDisplayDriver : ContentPartDisplayDriver<FormPart>
{
    public override IDisplayResult Edit(FormPart part, BuildPartEditorContext context)
    {
        return Initialize<FormPartEditViewModel>("FormPart_Fields_Edit", m =>
        {
            m.Action = part.Action;
            m.Method = part.Method;
            m.WorkflowTypeId = part.WorkflowTypeId;
            m.EncType = part.EncType;
            m.EnableAntiForgeryToken = part.EnableAntiForgeryToken;
            m.SaveFormLocation = part.SaveFormLocation;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(FormPart part, UpdatePartEditorContext context)
    {
        var viewModel = new FormPartEditViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        part.Action = viewModel.Action?.Trim();
        part.Method = viewModel.Method;
        part.WorkflowTypeId = viewModel.WorkflowTypeId;
        part.EncType = viewModel.EncType;
        part.EnableAntiForgeryToken = viewModel.EnableAntiForgeryToken;
        part.SaveFormLocation = viewModel.SaveFormLocation;

        return Edit(part, context);
    }
}
