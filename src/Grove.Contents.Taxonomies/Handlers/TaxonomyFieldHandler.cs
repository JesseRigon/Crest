using Microsoft.Extensions.Localization;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Taxonomies.Fields;
using Grove.Contents.Taxonomies.Settings;

namespace Grove.Contents.Taxonomies.Handlers;

public class TaxonomyFieldHandler : ContentFieldHandler<TaxonomyField>
{
    protected readonly IStringLocalizer S;

    public TaxonomyFieldHandler(IStringLocalizer<TaxonomyFieldHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidateContentFieldContext context, TaxonomyField field)
    {
        var settings = context.ContentPartFieldDefinition.GetSettings<TaxonomyFieldSettings>();

        if (settings.Required && field.TermContentItemIds.Length == 0)
        {
            context.Fail(S["A value is required for '{0}'", context.ContentPartFieldDefinition.DisplayName()], nameof(field.TermContentItemIds));
        }

        return Task.CompletedTask;
    }
}
