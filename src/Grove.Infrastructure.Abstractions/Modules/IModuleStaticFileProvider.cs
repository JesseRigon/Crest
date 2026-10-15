using Microsoft.Extensions.FileProviders;
using Grove.Modules.FileProviders;

namespace Grove.Modules;

/// <summary>
/// This custom <see cref="IFileProvider"/> implementation provides Di registration identification
/// for IStaticFileProviders that should be served via UseStaticFiles.
/// </summary>
public interface IModuleStaticFileProvider : IStaticFileProvider;
