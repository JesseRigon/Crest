using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Settings;
using Crest.Data.Migration;
using Crest.Modules;

namespace Crest.Contents.AuditTrail;

[RequireFeatures("Crest.AuditTrail")]
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("AuditTrailPart", part => part
            .Attachable()
            .WithDescription("Allows editors to enter a comment to be saved into the Audit Trail event when saving a content item."));

        return 1;
    }
}
