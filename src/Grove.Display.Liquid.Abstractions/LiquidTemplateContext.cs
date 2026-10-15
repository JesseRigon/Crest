using Fluid;

namespace Grove.Display.Liquid;

public class LiquidTemplateContext : TemplateContext
{
    public LiquidTemplateContext(IServiceProvider services, TemplateOptions options) : base(options)
    {
        Services = services;
    }

    public IServiceProvider Services { get; }

    public bool IsInitialized { get; set; }
}
