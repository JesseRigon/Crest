using Grove.Security.Permissions;

namespace Grove.Search.Lucene;

public static class LuceneSearchPermissions
{
    public static readonly Permission ManageLuceneIndexes = new("ManageLuceneIndexes", "Manage Lucene Indexes");
}
