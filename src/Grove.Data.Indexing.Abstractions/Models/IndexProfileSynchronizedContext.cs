namespace Grove.Data.Indexing.Models;

public sealed class IndexProfileSynchronizedContext
{
    public IndexProfile IndexProfile { get; }

    public IndexProfileSynchronizedContext(IndexProfile indexProfile)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);

        IndexProfile = indexProfile;
    }
}
