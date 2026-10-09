using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Models;
using Crest.ContentManagement.Metadata.Models;
using Crest.DisplayManagement.Views;
using Crest.Mvc.ModelBinding;

namespace Crest.ContentFields.Drivers;

public sealed class YoutubeFieldDisplayDriver : ContentFieldDisplayDriver<YoutubeField>
{
    internal readonly IStringLocalizer S;

    public YoutubeFieldDisplayDriver(IStringLocalizer<YoutubeFieldDisplayDriver> localizer)
    {
        S = localizer;
    }

    public override IDisplayResult Display(YoutubeField field, BuildFieldDisplayContext context)
    {
        return Initialize<YoutubeFieldDisplayViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    public override IDisplayResult Edit(YoutubeField field, BuildFieldEditorContext context)
    {
        return Initialize<EditYoutubeFieldViewModel>(GetEditorShapeType(context), model =>
       {
           model.RawAddress = field.RawAddress;
           model.EmbeddedAddress = field.EmbeddedAddress;
           model.Field = field;
           model.Part = context.ContentPart;
           model.PartFieldDefinition = context.PartFieldDefinition;
       });
    }

    public override async Task<IDisplayResult> UpdateAsync(YoutubeField field, UpdateFieldEditorContext context)
    {
        var model = new EditYoutubeFieldViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        var settings = context.PartFieldDefinition.GetSettings<YoutubeFieldSettings>();

        if (settings.Required && string.IsNullOrWhiteSpace(model.RawAddress))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RawAddress), S["A value is required for {0}.", context.PartFieldDefinition.DisplayName()]);
        }
        else
        {
            if (model.RawAddress != null)
            {
                var uri = new Uri(model.RawAddress);

                // If it is a url with QueryString.
                if (!string.IsNullOrWhiteSpace(uri.Query))
                {
                    var query = QueryHelpers.ParseQuery(uri.Query);
                    if (query.TryGetValue("v", out var values))
                    {
                        model.EmbeddedAddress = $"{uri.GetLeftPart(UriPartial.Authority)}/embed/{values}";
                    }
                    else
                    {
                        context.Updater.ModelState.AddModelError(Prefix, nameof(model.RawAddress), S["The format of the url is invalid"]);
                    }
                }
                else
                {
                    var path = uri.AbsolutePath.Split('?')[0];
                    model.EmbeddedAddress = $"{uri.GetLeftPart(UriPartial.Authority)}/embed/{path}";
                }

                field.RawAddress = model.RawAddress;
                field.EmbeddedAddress = model.EmbeddedAddress;
            }
            else
            {
                field.RawAddress = null;
                field.EmbeddedAddress = null;
            }
        }

        return Edit(field, context);
    }
}
