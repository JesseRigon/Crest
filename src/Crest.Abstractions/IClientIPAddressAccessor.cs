using System.Net;

namespace Crest;

public interface IClientIPAddressAccessor
{
    Task<IPAddress> GetIPAddressAsync();
}
