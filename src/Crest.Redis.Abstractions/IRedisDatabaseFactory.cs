using StackExchange.Redis;

namespace Crest.Redis;

/// <summary>
/// Factory allowing to share <see cref="IDatabase"/> instances across tenants.
/// </summary>
public interface IRedisDatabaseFactory
{
    Task<IDatabase> CreateAsync(RedisOptions options);
}
