using Microsoft.Extensions.FileProviders;
using Crest.Modules.FileProviders;

namespace Crest.Modules;

/// <summary>
/// This custom <see cref="IFileProvider"/> implementation provides Di registration identification
/// for IStaticFileProviders that should be served via UseStaticFiles.
/// </summary>
public class ModuleCompositeStaticFileProvider : CompositeFileProvider, IModuleStaticFileProvider
{
    public ModuleCompositeStaticFileProvider(params IStaticFileProvider[] fileProviders) : base(fileProviders)
    {
    }
    public ModuleCompositeStaticFileProvider(IEnumerable<IStaticFileProvider> fileProviders) : base(fileProviders)
    {
    }
}
