using System.Text.Encodings.Web;
using Fluid;

namespace Grove.Display.Liquid.Services;

/// <summary>Renders a parsed template with the tenant's clock, time zone and culture, and a <c>Model</c>.</summary>
public static class LiquidTemplateRendering
{
    public static async Task<string> RenderAsync(this IFluidTemplate template, TextEncoder encoder, LiquidTemplateContext context, object model)
    {
        await context.EnterScopeAsync(model);
        try
        {
            return await template.RenderAsync(context, encoder);
        }
        finally
        {
            context.ReleaseScope();
        }
    }

    public static async Task RenderAsync(this IFluidTemplate template, TextWriter writer, TextEncoder encoder, LiquidTemplateContext context, object model)
    {
        await context.EnterScopeAsync(model);
        try
        {
            await template.RenderAsync(writer, encoder, context);
        }
        finally
        {
            context.ReleaseScope();
        }
    }

    private static async ValueTask EnterScopeAsync(this LiquidTemplateContext context, object model)
    {
        await context.InitializeAsync();
        context.EnterChildScope();
        if (model is not null)
        {
            context.SetValue("Model", model);
        }
    }
}
