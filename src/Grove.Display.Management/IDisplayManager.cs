namespace Grove.Display.Management;

public interface IDisplayManager<TModel>
{
    Task<IShape> BuildDisplayAsync(TModel model, string displayType = "", string groupId = "");
}
