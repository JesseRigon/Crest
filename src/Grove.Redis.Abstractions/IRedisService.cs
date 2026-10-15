using Grove.Modules;
using StackExchange.Redis;

namespace Grove.Redis;

public interface IRedisService : IModularTenantEvents
{
    Task ConnectAsync();

    IConnectionMultiplexer Connection { get; }

    string InstancePrefix { get; }

    IDatabase Database { get; }
}
