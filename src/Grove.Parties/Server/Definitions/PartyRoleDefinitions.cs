using Grove.Parties.Constants;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Builders;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Contents.Management.Metadata.Models;
using Grove.Fields;
using Grove.Infrastructure.Settings;
using Grove.Models;

namespace Grove.Parties.Definitions;

public static class PartyRoleDefinitions
{
    public const string PartyField = "Party";

    // A party-role content type is a profile (Customer, Vendor, Employee...) that points at
    // exactly one party via its Party field. Each role declares its BASE (ruling 2026-10-14):
    // business relationships are organization roles (an individual is an Individual
    // organization), roles describing the person are person roles. The picker accepts the base
    // type only, and the content picker save guard enforces that on every save path. Domain
    // modules call this from their migrations so every role type carries the same
    // Party/Number/Terms shape.
    public static async Task CreatePartyRoleDefinitionsAsync(
        this IContentDefinitionManager contentDefinitionManager,
        string contentType,
        string displayName,
        string partDescription,
        string numberFieldDisplayName,
        string termsFieldDisplayName,
        PartyRoleBase roleBase,
        Func<ContentFieldSetDefinitionBuilder, ContentFieldSetDefinitionBuilder>? extraFields = null)
    {
        var baseType = roleBase.ContentType();
        await contentDefinitionManager.AlterFieldSetDefinitionAsync(contentType, part =>
        {
            part.Attachable()
                .Reusable(false)
                .WithDisplayName(displayName)
                .WithDescription(partDescription)
                .WithField(PartyField, field => field
                    .OfType("OptionListField")
                    .WithDisplayName(roleBase == PartyRoleBase.Organization ? "Organization" : "Person")
                    .WithDescription(roleBase == PartyRoleBase.Organization
                        ? "The organization this role belongs to (an individual is an Individual organization)."
                        : "The person this role belongs to.")
                    .WithPosition("0")
                    .MergeSettings<OptionListFieldSettings>(settings =>
                    {
                        settings.Multiple = false;
                        settings.Required = true;
                        settings.SourceKey = OptionSourceKeys.ForContentItems([baseType]);
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
            .WithFieldSet("Title", part => part.WithPosition("0"))
            .WithFieldSet(contentType, part => part.WithPosition("1")));
    }
}

/// <summary>What a party role is composed on (ruling 2026-10-14).</summary>
public enum PartyRoleBase
{
    /// <summary>A business relationship: Customer, Vendor, Partner, Lead, Prospect.</summary>
    Organization,

    /// <summary>A role describing the person: Employee, Member, Sales rep.</summary>
    Person,
}

public static class PartyRoleBases
{
    public static string ContentType(this PartyRoleBase roleBase) => roleBase == PartyRoleBase.Organization
        ? PartiesConstants.ContentTypes.Organization
        : PartiesConstants.ContentTypes.Person;

    /// <summary>The base a role type declared, read from its Party picker; null when the type
    /// is not a party role.</summary>
    public static async Task<PartyRoleBase?> GetAsync(IContentDefinitionManager definitions, string roleContentType)
    {
        var part = (await definitions.GetTypeDefinitionAsync(roleContentType))?.FieldSets
            .FirstOrDefault(candidate => candidate.Name == roleContentType);
        var field = part?.FieldSetDefinition.Fields.FirstOrDefault(candidate => candidate.Name == PartyRoleDefinitions.PartyField);
        var types = field?.GetSettings<OptionListFieldSettings>().ContentTypes() ?? [];
        return types switch
        {
            [PartiesConstants.ContentTypes.Organization] => PartyRoleBase.Organization,
            [PartiesConstants.ContentTypes.Person] => PartyRoleBase.Person,
            _ => null,
        };
    }
}
