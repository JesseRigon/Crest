namespace Grove.Display.Management;

public static class DisplayManagerExtensions
{
    public static Task<IShape> BuildDisplayAsync<TModel>(this IDisplayManager<TModel> displayManager, string displayType = "", string groupId = "") where TModel : new()
        => displayManager.BuildDisplayAsync(new TModel(), displayType, groupId);
}
