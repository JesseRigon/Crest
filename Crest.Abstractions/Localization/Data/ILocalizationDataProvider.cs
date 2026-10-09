namespace Crest.Localization.Data;

public interface ILocalizationDataProvider
{
    Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync();
}
