using Grove.Routes.Auto.Models;
using Grove.Data.Indexing;

namespace Grove.Routes.Auto.Indexing;

public class AutorouteFieldSetIndexHandler : ContentFieldSetIndexHandler<AutorouteFieldSet>
{
    public override Task BuildIndexAsync(AutorouteFieldSet part, BuildFieldSetIndexContext context)
    {
        var options = context.Settings.ToOptions()
            & ~DocumentIndexOptions.Sanitize
            ;

        foreach (var key in context.Keys)
        {
            context.DocumentIndex.Set(key, part.Path, options);
        }

        return Task.CompletedTask;
    }
}
