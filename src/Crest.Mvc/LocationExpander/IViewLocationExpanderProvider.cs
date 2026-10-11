using Microsoft.AspNetCore.Mvc.Razor;

namespace Crest.Mvc.LocationExpander;

public interface IViewLocationExpanderProvider : IViewLocationExpander
{
    int Priority { get; }
}
