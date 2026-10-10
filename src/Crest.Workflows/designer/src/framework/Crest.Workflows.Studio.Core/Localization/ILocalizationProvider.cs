namespace Crest.Workflows.Studio.Localization;

/// <summary>
/// Defines the contract for localization provider.
/// </summary>
public interface ILocalizationProvider
{
    string GetTranslation(string key);
}