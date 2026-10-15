using Grove.Display.Management.Views;

namespace Grove.Display.Management.Handlers;

/// <summary>A display driver: adds the shapes of a model's display (docs/MVC/shape-engine.md).</summary>
public interface IDisplayDriver<in TModel, TDisplayContext>
    where TDisplayContext : BuildDisplayContext
{
    Task<IDisplayResult> BuildDisplayAsync(TModel model, TDisplayContext context);
}

public interface IDisplayDriver<in TModel> : IDisplayDriver<TModel, BuildDisplayContext>;
