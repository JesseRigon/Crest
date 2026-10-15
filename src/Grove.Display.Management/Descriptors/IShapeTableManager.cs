namespace Grove.Display.Management.Descriptors;

public interface IShapeTableManager
{
    Task<ShapeTable> GetShapeTableAsync(string themeId);
}
