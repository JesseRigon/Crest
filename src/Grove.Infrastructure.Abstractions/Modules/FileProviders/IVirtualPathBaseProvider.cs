using Microsoft.AspNetCore.Http;

namespace Grove.Modules.FileProviders;

public interface IVirtualPathBaseProvider
{
    PathString VirtualPathBase { get; }
}
