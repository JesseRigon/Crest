using Microsoft.Extensions.Logging;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Modules;

namespace Grove.Contents.Types.Services;

public class StereotypeService : IStereotypeService
{
    private readonly IEnumerable<IStereotypesProvider> _providers;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ILogger<StereotypeService> _logger;

    public StereotypeService(
        IEnumerable<IStereotypesProvider> providers,
        IContentDefinitionManager contentDefinitionManager,
        ILogger<StereotypeService> logger)
    {
        _providers = providers;
        _contentDefinitionManager = contentDefinitionManager;
        _logger = logger;
    }

    public async Task<IEnumerable<StereotypeDescription>> GetStereotypesAsync()
    {
        var providerStereotypes = (await _providers.InvokeAsync(provider => provider.GetStereotypesAsync(), _logger)).ToList();

        var stereotypes = providerStereotypes.Select(providerStereotype => providerStereotype.Stereotype)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var contentType in await _contentDefinitionManager.ListTypeDefinitionsAsync())
        {
            if (!contentType.TryGetStereotype(out var stereotype) ||
                stereotypes.Contains(stereotype))
            {
                continue;
            }

            providerStereotypes.Add(new StereotypeDescription
            {
                Stereotype = stereotype,
                DisplayName = stereotype,
            });
        }

        return providerStereotypes.OrderBy(x => x.DisplayName);
    }
}
