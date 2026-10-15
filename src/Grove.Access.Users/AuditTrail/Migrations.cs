using Grove.AuditTrail.Models;
using Grove.Data.Migration;
using Grove.Access.Users.AuditTrail.Indexes;
using YesSql.Sql;

namespace Grove.Access.Users.AuditTrail;

public sealed class Migrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AuditTrailUserEventIndex>(table => table
            .Column<string>(nameof(AuditTrailUserEventIndex.UserId))
            .Column<bool>(nameof(AuditTrailUserEventIndex.HasUserSnapshot)),
            collection: AuditTrailEvent.Collection
        );

        return 1;
    }
}
