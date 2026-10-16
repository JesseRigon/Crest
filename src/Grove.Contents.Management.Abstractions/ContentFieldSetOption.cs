namespace Grove.Contents.Management;

public class ContentFieldSetOption : ContentFieldSetOptionBase
{
    private readonly List<Type> _handlers = [];

    public ContentFieldSetOption(Type contentFieldSetType) : base(contentFieldSetType)
    {
    }

    public IReadOnlyList<Type> Handlers => _handlers;

    internal void AddHandler(Type type)
    {
        _handlers.Add(type);
    }

    internal void RemoveHandler(Type type)
    {
        _handlers.Remove(type);
    }
}
