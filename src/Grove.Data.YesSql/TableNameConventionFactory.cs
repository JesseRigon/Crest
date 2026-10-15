using Grove.Data.YesSql;
using YesSql;

namespace Grove.Data;

public class TableNameConventionFactory : ITableNameConventionFactory
{
    public ITableNameConvention Create(DatabaseTableOptions options)
    {
        return new DefaultTableNameConvention(options);
    }
}
