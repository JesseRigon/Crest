namespace Crest.DisplayManagement.TagHelpers;

public interface ITagHelpersProvider
{
    IEnumerable<Type> GetTypes();
}
