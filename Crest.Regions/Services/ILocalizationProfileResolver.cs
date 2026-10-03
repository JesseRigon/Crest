using Crest.Regions.Models;
using OrchardCore.ContentManagement;

namespace Crest.Regions.Services;

public sealed record LocalizationProfileModel(
    string ContentItemId,
    string Key,
    string DisplayText,
    string? DefaultCountry,
    string? MeasurementSystem,
    string? TimeZone,
    string? Language,
    bool Enabled);

/// <summary>
/// Party → context. Reads <see cref="CrestLocalizationProfileReferencePart"/> off the given
/// item; absent or dangling means "no context", never an exception, so an item whose
/// owning module never attached the reference keeps working with tenant defaults.
/// </summary>
public interface ILocalizationProfileResolver
{
    Task<LocalizationProfileModel?> ResolveAsync(ContentItem? carrier, CancellationToken cancellationToken = default);
    Task<LocalizationProfileModel?> GetAsync(string contentItemId, CancellationToken cancellationToken = default);
    Task<LocalizationProfileModel?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocalizationProfileModel>> ListAsync(CancellationToken cancellationToken = default);
}
