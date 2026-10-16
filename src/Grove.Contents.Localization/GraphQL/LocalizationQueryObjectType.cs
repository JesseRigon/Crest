using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL;
using Grove.Contents.Localization.Models;
using Grove.Contents.Management;
using Grove.Contents.Management.GraphQL.Queries.Types;

namespace Grove.Contents.Localization.GraphQL;

public class LocalizationQueryObjectType : ObjectGraphType<LocalizationFieldSet>
{
    public LocalizationQueryObjectType(IStringLocalizer<LocalizationQueryObjectType> S)
    {
        Name = "LocalizationFieldSet";
        Description = S["Localization cultures for your content item."];

        Field(x => x.Culture).Description(S["The culture for your content item."]);
        Field(x => x.LocalizationSet).Description(S["The localization set for your content item."]);

        Field<ListGraphType<ContentItemInterface>, IEnumerable<ContentItem>>("Localizations")
            .Description(S["The localizations of the content item."])
            .Argument<StringGraphType>("culture", "the culture of the content item")
            .ResolveLockedAsync(GetContentItemsByLocalizationSetAsync);
    }

    private static async ValueTask<IEnumerable<ContentItem>> GetContentItemsByLocalizationSetAsync(IResolveFieldContext<LocalizationFieldSet> context)
    {
        var culture = context.GetArgument<string>("culture");
        var contentLocalizationManager = context.RequestServices.GetService<IContentLocalizationManager>();

        if (culture != null)
        {
            var contentItem = await contentLocalizationManager.GetContentItemAsync(context.Source.LocalizationSet, culture);

            return contentItem != null ? new[] { contentItem } : [];
        }

        return await contentLocalizationManager.GetItemsForSetAsync(context.Source.LocalizationSet);
    }
}
