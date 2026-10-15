namespace Grove.DisplayManagement.TagHelpers;

public interface ITagHelpersProvider
{
    IEnumerable<Type> GetTypes();
}
