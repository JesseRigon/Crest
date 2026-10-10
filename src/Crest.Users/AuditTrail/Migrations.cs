using Crest.AuditTrail.Models;
using Crest.Data.Migration;
using Crest.Users.AuditTrail.Indexes;
using YesSql.Sql;

namespace Crest.Users.AuditTrail;

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
