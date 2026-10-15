namespace Grove.Contents.Management.Handlers;

public interface IContentFieldHandlerResolver
{
    IList<IContentFieldHandler> GetHandlers(string fieldName);
}
