using System.Globalization;
using Fluid;
using Microsoft.Extensions.Options;
using Grove.Display.Liquid.Values;

namespace Grove.Display.Liquid.Services;

/// <summary>
/// The Liquid language configuration shared by every template: the sandbox bounds and the
/// <c>Culture</c> value. Tenant Liquid gets no request values (<c>Request</c>, <c>HttpContext</c>):
/// a template reads what it needs through the values and accessors modules register.
/// </summary>
public sealed class LiquidTemplateOptionsSetup : IConfigureOptions<TemplateOptions>
{
    public void Configure(TemplateOptions options)
    {
        // The sandbox's resource bound: Liquid is tenant-authored (title patterns, templates),
        // so a template cannot loop or recurse without end. What it can REACH is bounded by
        // the data-loading filters, which go through the caller's access decision
        // (docs/content-engine.md › Liquid over content).
        options.MaxSteps = 1_000_000;
        options.MaxRecursion = 100;

        options.MemberAccessStrategy.Register<CultureInfo>();
        options.Scope.SetValue("Culture", new CultureValue());
    }
}
