using Microsoft.Extensions.Localization;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata.Models;
using Grove.Taxonomies.Fields;
using Grove.Taxonomies.Settings;

namespace Grove.Taxonomies.Handlers;

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
