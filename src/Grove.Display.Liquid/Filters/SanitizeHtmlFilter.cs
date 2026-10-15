using Fluid;
using Fluid.Values;
using Grove.Infrastructure.Html;
using Grove.Display.Liquid;

namespace Grove.Display.Liquid.Filters;

public class SanitizeHtmlFilter : ILiquidFilter
{
    private readonly IHtmlSanitizerService _htmlSanitizerService;

    public SanitizeHtmlFilter(IHtmlSanitizerService htmlSanitizerService)
    {
        _htmlSanitizerService = htmlSanitizerService;
    }
    public ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext ctx)
    {
        var html = input.ToStringValue();

        html = _htmlSanitizerService.Sanitize(html);

        return ValueTask.FromResult<FluidValue>(new StringValue(html));
    }
}
