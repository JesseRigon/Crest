namespace Grove.Contents.Management.Handlers;

public interface IContentPartHandlerResolver
{
    IList<IContentPartHandler> GetHandlers(string partName);
}
