using Crest.Data.YesSql;
using YesSql;

namespace Crest.Data;

public class TableNameConventionFactory : ITableNameConventionFactory
{
    public ITableNameConvention Create(DatabaseTableOptions options)
    {
        return new DefaultTableNameConvention(options);
    }
}
