using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Settings;
using Crest.Data.Migration;

namespace Crest.ReCaptcha.Forms;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("ReCaptchaPart", part => part
            .WithDescription("Provides captcha properties."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync("ReCaptcha", type => type
            .WithPart("ReCaptchaPart")
            .Stereotype("Widget"));

        return 1;
    }
}
