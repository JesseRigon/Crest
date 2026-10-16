namespace Grove.Contents.Management.Handlers;

public interface IContentFieldSetHandlerResolver
{
    IList<IContentFieldSetHandler> GetHandlers(string fieldSetName);
}
