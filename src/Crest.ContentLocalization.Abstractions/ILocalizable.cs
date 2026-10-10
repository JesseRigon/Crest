namespace Crest.ContentLocalization;

public interface ILocalizable
{
    string LocalizationSet { get; }
    string Culture { get; }
}
