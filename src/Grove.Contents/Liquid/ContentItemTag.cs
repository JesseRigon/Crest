using System.Text.Encodings.Web;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using Grove.DisplayManagement.Liquid.Tags;

namespace Grove.Contents.Liquid;

public class ContentItemTag
{
    private static readonly FilterArgument s_typeArgument = new("type", new LiteralExpression(StringValue.Create("contentitem")));

    public static ValueTask<Completion> WriteToAsync(IReadOnlyList<FilterArgument> argumentsList, TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        var list = new List<FilterArgument>(argumentsList)
        {
            s_typeArgument,
        };

        return ShapeTag.WriteToAsync(list, writer, encoder, context);
    }
}
