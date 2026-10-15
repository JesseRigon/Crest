using Fluid;
using Fluid.Ast;
using Microsoft.Extensions.Options;
using Parlot.Fluent;

namespace Grove.Display.Liquid.Services;

/// <summary>
/// The shared Liquid language (docs/MVC/shape-engine.md › Liquid without LiquidPage): Fluid's
/// own tags with the tenant's parser options (<c>Grove_Liquid</c>), and no view tags. Every caller
/// parses with it: Autoroute and title patterns, HTML and Markdown bodies, workflows, queries.
/// The tree templates have their own parser (Grove.Display.Templates' <c>TemplateTreeParser</c>).
/// </summary>
public sealed class LiquidParser : FluidParser
{
    public LiquidParser(IOptions<FluidParserOptions> fluidParserOptions)
        : base(fluidParserOptions?.Value ?? throw new ArgumentNullException(nameof(fluidParserOptions)))
    {
    }

    public Parser<IReadOnlyList<FilterArgument>> ArgumentsListParser => ArgumentsList;
}
