using Crest.Modules;
using StackExchange.Redis;

namespace Crest.Redis;

public interface IRedisService : IModularTenantEvents
{
    Task ConnectAsync();

    IConnectionMultiplexer Connection { get; }

    string InstancePrefix { get; }

    IDatabase Database { get; }
}
