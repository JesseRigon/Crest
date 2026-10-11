using Crest.Media.Files;
using HeyRed.Mime;
using Microsoft.Extensions.Options;

namespace Crest.Media.Scanning.Detection;

/// <summary>
/// Type detection with libmagic (requirement 91, chosen in "Scanner candidates"), through the
/// MIT <c>Mime</c> package, which ships the native library and its database for every server
/// platform. A missing native library or database is a failure, which keeps the upload in
/// quarantine; it never falls back silently.
/// </summary>
public sealed class LibmagicFileTypeDetector(IOptions<UploadScanningOptions> options) : IFileTypeDetector
{
    public const string DetectorName = "libmagic";

    private readonly UploadScanningOptions _options = options.Value;

    public string Name => DetectorName;

    public async Task<string?> DetectAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var buffer = new byte[Math.Max(_options.LibmagicReadBytes, 4096)];
        var length = await content.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);

        if (length == 0)
        {
            return null;
        }

        try
        {
            // A Magic handle is not thread-safe: one per call.
            using var magic = new Magic(MagicOpenFlags.MAGIC_MIME_TYPE, _options.LibmagicDatabasePath ?? MimeGuesser.MagicFilePath);
            var type = magic.Read(buffer, length);

            return string.IsNullOrWhiteSpace(type) ? null : type.Trim();
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or MagicException)
        {
            throw new InvalidOperationException(
                $"libmagic is configured as the type detector but could not run ({exception.Message}). Check the native library and its database for this platform.",
                exception);
        }
    }
}
