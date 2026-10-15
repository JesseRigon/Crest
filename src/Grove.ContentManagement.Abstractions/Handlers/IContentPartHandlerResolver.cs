namespace Grove.ContentManagement.Handlers;

public interface IContentPartHandlerResolver
{
    IList<IContentPartHandler> GetHandlers(string partName);
}
