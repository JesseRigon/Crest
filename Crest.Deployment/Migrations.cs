using Crest.Data.Migration;
using Crest.Deployment.Indexes;
using YesSql.Sql;

namespace Crest.Deployment;

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
