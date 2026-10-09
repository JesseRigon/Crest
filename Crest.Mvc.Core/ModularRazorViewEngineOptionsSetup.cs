using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Options;
using Crest.Mvc.LocationExpander;

namespace Crest.Mvc;

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
