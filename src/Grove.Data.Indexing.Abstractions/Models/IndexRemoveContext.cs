namespace Grove.Data.Indexing.Models;

public class IndexRemoveContext(string indexFullName)
{
    public string IndexFullName { get; } = indexFullName;
}
