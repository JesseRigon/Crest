namespace Crest.DisplayManagement.Descriptors;

public interface IShapeTableManager
{
    Task<ShapeTable> GetShapeTableAsync(string themeId);
}
