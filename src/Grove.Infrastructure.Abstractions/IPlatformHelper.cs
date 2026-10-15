using Microsoft.AspNetCore.Http;

namespace Grove;

public interface IPlatformHelper
{
    HttpContext HttpContext { get; }
}
