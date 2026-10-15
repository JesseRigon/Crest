using Grove.Data.Types;
using Xunit;

namespace Grove.Server.Tests;

// The value type registry (docs/blazor-display.md, module homes): the base types, and a module's
// own type registered beside them.
public sealed class ValueTypeRegistryTests
{
    [Fact]
    public void BaseTypesAnswerByNameAndByField()
    {
        var registry = new ValueTypeRegistry([]);

        Assert.True(registry.TryGet("text", out var text));
        Assert.Equal("TextFieldEditor", text.Editor);
        Assert.True(registry.TryGetByField("HtmlBodyPart", out var html));
        Assert.Equal("Html", html.Name);
    }

    [Fact]
    public void AModulesTypeIsRegisteredBesideTheBaseOnes()
    {
        var registry = new ValueTypeRegistry([new ValueTypeDescriptor("Money", ValueTypeKinds.Object, ValueTypeKinds.Object, ["PriceField"])]);

        Assert.True(registry.TryGetByField("PriceField", out var money));
        Assert.Equal("Money", money.Name);
        Assert.Contains(registry.Types, type => type.Name == "Text");
    }
}
