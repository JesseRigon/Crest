using Microsoft.AspNetCore.Mvc.Razor;

namespace Grove.Mvc.LocationExpander;

public interface IViewLocationExpanderProvider : IViewLocationExpander
{
    int Priority { get; }
}
