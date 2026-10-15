using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Options;
using Grove.Mvc.LocationExpander;

namespace Grove.Mvc;

public sealed class ModularRazorViewEngineOptionsSetup : IConfigureOptions<RazorViewEngineOptions>
{
    public ModularRazorViewEngineOptionsSetup()
    {
    }

    public void Configure(RazorViewEngineOptions options)
    {
        options.ViewLocationExpanders.Add(new CompositeViewLocationExpanderProvider());
    }
}
