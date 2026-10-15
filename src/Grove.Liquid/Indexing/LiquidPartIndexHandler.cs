using Grove.Indexing;
using Grove.Liquid.Models;

namespace Grove.Liquid.Indexing;

public class LiquidPartIndexHandler : ContentPartIndexHandler<LiquidPart>
{
    public override Task BuildIndexAsync(LiquidPart part, BuildPartIndexContext context)
    {
        var options = context.Settings.ToOptions() | DocumentIndexOptions.Sanitize;

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, part.Liquid, options);
        }

        return Task.CompletedTask;
    }
}
