using Grove.Communications.Delivery;
using Grove.Data.Migration;
using YesSql.Sql;

namespace Grove.Communications.Delivery;

public sealed class DeliveryMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<DeliveryRecordIndex>(table => table
            .Column<string>("DeliveryId", column => column.WithLength(26))
            .Column<string>("Channel", column => column.WithLength(32))
            .Column<string>("Provider", column => column.WithLength(100))
            .Column<string>("Outcome", column => column.WithLength(32))
            .Column<string>("Recipient", column => column.WithLength(DeliveryRecordIndex.RecipientLength))
            .Column<DateTime>("CreatedUtc"),
            collection: CommunicationsConstants.DeliveryCollection);

        await SchemaBuilder.AlterIndexTableAsync<DeliveryRecordIndex>(table => table
            .CreateIndex("IDX_DeliveryRecordIndex_Created", "DocumentId", "CreatedUtc", "Channel", "Outcome"),
            collection: CommunicationsConstants.DeliveryCollection);

        return 1;
    }
}
