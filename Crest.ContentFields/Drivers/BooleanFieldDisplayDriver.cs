using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.DisplayManagement.Views;

namespace Crest.ContentFields.Drivers;

public sealed class BooleanFieldDisplayDriver : ContentFieldDisplayDriver<BooleanField>
{
    public override IDisplayResult Display(BooleanField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayBooleanFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    public override IDisplayResult Edit(BooleanField field, BuildFieldEditorContext context)
    {
        return Initialize<EditBooleanFieldViewModel>(GetEditorShapeType(context), model =>
        {
            model.Value = (context.IsNew == false) ?
                field.Value : context.PartFieldDefinition.GetSettings<BooleanFieldSettings>().DefaultValue;

            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(BooleanField field, UpdateFieldEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(field, Prefix, f => f.Value);

        return Edit(field, context);
    }
}
