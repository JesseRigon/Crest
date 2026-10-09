using Microsoft.Extensions.Caching.Distributed;
using Crest.Redis.Services;

namespace Crest.Tests.Modules.Crest.Redis;

public class RedisCacheWrapperTests
{
    [Fact]
    public void RedisCacheWrapper_Default_NotDisposeInnerCache()
    {
        var redisCache = new Mock<IDistributedCache>();
        var disposable = redisCache.As<IDisposable>();

        object testObject = new RedisCacheWrapper(redisCache.Object);

        // If the RedisCacheWrapper ever implements IDisposable, make sure it does 'not'
        // dispose the inner cache object. 
        (testObject as IDisposable)?.Dispose();

        disposable.Verify(disposableMock => disposableMock.Dispose(), Times.Never());
    }
}
