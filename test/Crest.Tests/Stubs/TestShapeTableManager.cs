using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Theming;
using Crest.Environment.Extensions;

namespace Crest.Tests.Stubs;

public class TestShapeTableManager(ShapeTable defaultShapeTable) : IShapeTableManager
{
    private readonly ShapeTable _defaultShapeTable = defaultShapeTable;

    public ShapeTable GetShapeTable(string _)
        => _defaultShapeTable;

    public Task<ShapeTable> GetShapeTableAsync(string _)
        => Task.FromResult(_defaultShapeTable);
}

public class MockThemeManager(IExtensionInfo des) : IThemeManager
{
    private readonly IExtensionInfo _dec = des;

    public Task<IExtensionInfo> GetThemeAsync()
    {
        return Task.FromResult(_dec);
    }
}
