namespace Crest.ContentManagement.Handlers;

public interface IContentFieldHandlerResolver
{
    IList<IContentFieldHandler> GetHandlers(string fieldName);
}
