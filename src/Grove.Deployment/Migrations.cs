using Grove.Data.Migration;
using Grove.Deployment.Indexes;
using YesSql.Sql;

namespace Grove.Deployment;

public sealed class Migrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<DeploymentPlanIndex>(table => table
            .Column<string>("Name")
        );

        return 1;
    }
}
