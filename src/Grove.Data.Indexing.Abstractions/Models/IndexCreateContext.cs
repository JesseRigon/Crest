namespace Grove.Data.Indexing.Models;

public class IndexCreateContext
{
    public IndexProfile IndexProfile { get; }

    public IndexCreateContext(IndexProfile indexProfile)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);

        IndexProfile = indexProfile;
    }
}
