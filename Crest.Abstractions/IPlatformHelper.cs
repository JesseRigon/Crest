using Microsoft.AspNetCore.Http;

namespace Crest;

public interface IPlatformHelper
{
    HttpContext HttpContext { get; }
}
