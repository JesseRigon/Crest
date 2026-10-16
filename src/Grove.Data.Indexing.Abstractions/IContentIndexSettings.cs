namespace Grove.Data.Indexing;

/// <summary>
/// Represents the indexing settings for a field set or a field.
/// </summary>
public interface IContentIndexSettings
{
    bool Included { get; set; }

    DocumentIndexOptions ToOptions();
}
