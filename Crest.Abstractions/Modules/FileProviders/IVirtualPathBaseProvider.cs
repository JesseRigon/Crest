using Microsoft.AspNetCore.Http;

namespace Crest.Modules.FileProviders;

public interface IVirtualPathBaseProvider
{
    PathString VirtualPathBase { get; }
}
