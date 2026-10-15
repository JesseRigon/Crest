using Grove.Data.Indexing;

namespace Grove.Integrations.AI.Azure.Models;

public class AzureAISearchContentIndexSettings : IContentIndexSettings
{
    public bool Included { get; set; }

    public DocumentIndexOptions ToOptions()
    {
        var options = DocumentIndexOptions.None;

        return options;
    }
}
