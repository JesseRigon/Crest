using System.Reflection;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Descriptors;
using Grove.DisplayManagement.Descriptors.ShapeAttributeStrategy;
using Grove.DisplayManagement.Implementation;
using Grove.DisplayManagement.Shapes;

namespace Grove.Tests.DisplayManagement.Descriptors;

public class ShapeAttributeBindingStrategyTests
{
    [Fact]
    public async Task StaticShapeMethodsDoNotResolveTheirProviderInstance_Default_Succeeds()
    {
        var methodInfo = typeof(StaticShapeProvider).GetMethod(nameof(StaticShapeProvider.StaticShape));
        var occurrence = new ShapeAttributeOccurrence(new ShapeAttribute(), methodInfo, typeof(StaticShapeProvider));
        var createDelegateMethod = typeof(ShapeAttributeBindingStrategy).GetMethod("CreateDelegate", BindingFlags.NonPublic | BindingFlags.Static);

        var binding = (Func<DisplayContext, Task<IHtmlContent>>)createDelegateMethod.Invoke(null, [occurrence]);
        var serviceProvider = new ThrowingServiceProvider();

        var result = await binding(new DisplayContext
        {
            ServiceProvider = serviceProvider,
            Value = new Shape(),
        });

        Assert.Equal("<span>static</span>", ToHtmlString(result));
        Assert.Equal(0, serviceProvider.ShapeProviderRequests);
    }

    private static string ToHtmlString(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private sealed class StaticShapeProvider : IShapeAttributeProvider
    {
        [global::Grove.DisplayManagement.Shape]
        public static HtmlString StaticShape() => new HtmlString("<span>static</span>");
    }

    private sealed class ThrowingServiceProvider : IServiceProvider
    {
        public int ShapeProviderRequests { get; private set; }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(StaticShapeProvider))
            {
                ShapeProviderRequests++;
                throw new InvalidOperationException("Static shape providers should not be resolved.");
            }

            return null;
        }
    }
}
