namespace Crest.Indexing.Models;

public class IndexRemoveContext(string indexFullName)
{
    public string IndexFullName { get; } = indexFullName;
}
