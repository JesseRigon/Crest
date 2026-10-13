using Crest.ContentManagement.Metadata;
using Crest.Data.Migration;
using Crest.Modules;
using Crest.Parties.Definitions;
using Crest.Parties.PartyTypes;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Parties.SampleRole;

// A party role of Crest's own, as a fixture (docs/architecture.md › Standing alone, 3): one
// role content type composed the way a host module composes its roles, so the generic role
// pages - the /Parties/{key} page, the All Parties pane, the menu item, the route gate - can
// be exercised with no host module present. Off unless a test or demo enables it.
[Feature(SampleRoleConstants.FeatureId)]
public sealed class SampleRoleStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<SampleRoleMigrations>();
        services.AddScoped<IPartyTypeProvider, SampleRoleTypeProvider>();
    }
}

public static class SampleRoleConstants
{
    public const string FeatureId = "Crest.Parties.SampleRole";
    public const string ContentType = "SampleRole";
    public const string TypeKey = "sample-roles";
}

public sealed class SampleRoleMigrations(IContentDefinitionManager contentDefinitionManager) : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await contentDefinitionManager.CreatePartyRoleDefinitionsAsync(
            SampleRoleConstants.ContentType,
            "Sample role",
            "A sample party role for exercising the generic role pages.",
            "Sample number",
            "Sample terms");

        return 1;
    }
}

public sealed class SampleRoleTypeProvider : IPartyTypeProvider
{
    public IEnumerable<PartyTypeDescriptor> GetTypes() =>
    [
        new()
        {
            Key = SampleRoleConstants.TypeKey,
            DisplayName = "Sample roles",
            ContentType = SampleRoleConstants.ContentType,
            Icon = "fa fa-flask",
            Position = 900,
        },
    ];
}
