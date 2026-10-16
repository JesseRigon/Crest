using Grove.Integrations.Facebook.Models;
using Grove.Infrastructure;

namespace Grove.Integrations.Facebook.Services;

/// <summary>
/// Sends server-side events to the Meta Conversions API for the configured pixel.
/// See https://developers.facebook.com/docs/marketing-api/conversions-api.
/// </summary>
public interface IMetaConversionsApiService
{
    /// <summary>
    /// Sends a single event through the Conversions API connection. Returns a failed
    /// <see cref="Result"/> when the pixel is not set, the connection has no token, or the call fails.
    /// </summary>
    Task<Result> SendEventAsync(MetaConversionEvent conversionEvent, CancellationToken cancellationToken = default);
}
