namespace Grove.Display.Management.Handlers;

public interface IDisplayHandler<TModel>
{
    Task BuildDisplayAsync(TModel model, BuildDisplayContext context);
}
