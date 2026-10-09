namespace Crest.Infrastructure.Entities;

public sealed class DeletedContext<T> : HandlerContextBase<T>
{
    public DeletedContext(T model)
        : base(model)
    {
    }
}
