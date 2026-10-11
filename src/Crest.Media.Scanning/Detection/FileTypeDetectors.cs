using Crest.Media.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crest.Media.Scanning.Detection;

/// <summary>The host's configured type detector: libmagic, or the built-in signatures when none is set.</summary>
public static class FileTypeDetectors
{
    public static IFileTypeDetector Resolve(IServiceProvider services)
    {
        var name = services.GetRequiredService<IOptions<UploadScanningOptions>>().Value.TypeDetector;

        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, SignatureFileTypeDetector.DetectorName, StringComparison.OrdinalIgnoreCase))
        {
            return services.GetRequiredService<SignatureFileTypeDetector>();
        }

        if (string.Equals(name, LibmagicFileTypeDetector.DetectorName, StringComparison.OrdinalIgnoreCase))
        {
            return services.GetRequiredService<LibmagicFileTypeDetector>();
        }

        throw new InvalidOperationException($"The type detector '{name}' is unknown; use '{LibmagicFileTypeDetector.DetectorName}' or '{SignatureFileTypeDetector.DetectorName}'.");
    }
}
