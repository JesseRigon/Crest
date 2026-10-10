using YesSql;

namespace Crest.Data.YesSql;

public interface ITableNameConventionFactory
{
    ITableNameConvention Create(DatabaseTableOptions options);
}
