using System.Net;

namespace Grove;

public interface IClientIPAddressAccessor
{
    Task<IPAddress> GetIPAddressAsync();
}
