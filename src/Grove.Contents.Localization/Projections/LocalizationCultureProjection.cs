using System.Globalization;
using Grove.Contents.Localization.Models;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Data.Localization;

namespace Grove.Contents.Localization.Projections;

/// <summary>An item's culture (a read projection): its Localization field set's culture.</summary>
public sealed class LocalizationCultureProjection : ContentFieldSetProjection<LocalizationFieldSet>
{
    protected override Task ProjectAsync(ContentItemAspectContext context, LocalizationFieldSet part)
    {
        return context.ForAsync<CultureAspect>(cultureAspect =>
        {
            if (part.Culture != null)
            {
                cultureAspect.Culture = CultureInfo.GetCultureInfo(part.Culture);
                cultureAspect.HasCulture = true;
            }

            return Task.CompletedTask;
        });
    }
}
