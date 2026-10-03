using Crest.Parties.Constants;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Settings;

namespace Crest.Parties.Definitions;

public static class PartyRoleDefinitions
{
    // A party-role content type is a profile (Customer, Vendor, Partner, Employee...)
    // that points at exactly one Person or Organization via its Party field. Domain
    // modules call this from their migrations so every role type carries the same
    // Party/Number/Terms base shape and stays consistent with the party model.
    public static async Task CreatePartyRoleDefinitionsAsync(
        this IContentDefinitionManager contentDefinitionManager,
        string contentType,
        string displayName,
        string partDescription,
        string numberFieldDisplayName,
        string termsFieldDisplayName,
        Func<ContentPartDefinitionBuilder, ContentPartDefinitionBuilder>? extraFields = null)
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(contentType, part =>
        {
            part.Attachable()
                .Reusable(false)
                .WithDisplayName(displayName)
                .WithDescription(partDescription)
                .WithField("Party", field => field
                    .OfType("ContentPickerField")
                    .WithDisplayName("Party")
                    .WithDescription("The Person or Organization this role belongs to.")
                    .WithPosition("0")
                    .MergeSettings<ContentPickerFieldSettings>(settings =>
                    {
                        settings.Multiple = false;
                        settings.DisplayAllContentTypes = false;
                        settings.DisplayedContentTypes =
                            [PartiesConstants.ContentTypes.Person, PartiesConstants.ContentTypes.Organization];
                    }))
                .WithField("Number", field => field
                    .OfType("TextField")
                    .WithDisplayName(numberFieldDisplayName)
                    .WithPosition("1"))
                .WithField("Terms", field => field
                    .OfType("TextField")
                    .WithDisplayName(termsFieldDisplayName)
                    .WithPosition("2"));

            extraFields?.Invoke(part);
        });

        await contentDefinitionManager.AlterTypeDefinitionAsync(contentType, type => type
            .WithDisplayName(displayName)
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(contentType, part => part.WithPosition("1")));
    }
}
