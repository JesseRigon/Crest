using StackExchange.Redis;

namespace Grove.Integrations.Redis;

/// <summary>
/// Factory allowing to share <see cref="IDatabase"/> instances across tenants.
/// </summary>
public interface IRedisDatabaseFactory
{
    Task<IDatabase> CreateAsync(RedisOptions options);
}
