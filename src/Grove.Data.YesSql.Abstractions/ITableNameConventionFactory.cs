using YesSql;

namespace Grove.Data.YesSql;

public interface ITableNameConventionFactory
{
    ITableNameConvention Create(DatabaseTableOptions options);
}
