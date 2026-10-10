using Fluid;
using Fluid.Values;
using Crest.Infrastructure.Html;
using Crest.Liquid;

namespace Crest.DisplayManagement.Liquid.Filters;

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
