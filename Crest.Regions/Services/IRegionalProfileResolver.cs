using Crest.Regions.Models;
using OrchardCore.ContentManagement;

namespace Crest.Regions.Services;

public sealed record RegionalProfileModel(
    string ContentItemId,
    string Key,
    string DisplayText,
    string? DefaultCountry,
    string? MeasurementSystem,
    string? TimeZone,
    string? Language,
    bool Enabled);

/// <summary>
/// Party → context. Reads <see cref="CrestRegionalProfileReferencePart"/> off the given
/// item; absent or dangling means "no context", never an exception, so an item whose
/// owning module never attached the reference keeps working with tenant defaults.
/// </summary>
public interface IRegionalProfileResolver
{
    Task<RegionalProfileModel?> ResolveAsync(ContentItem? carrier, CancellationToken cancellationToken = default);
    Task<RegionalProfileModel?> GetAsync(string contentItemId, CancellationToken cancellationToken = default);
    Task<RegionalProfileModel?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegionalProfileModel>> ListAsync(CancellationToken cancellationToken = default);
}
