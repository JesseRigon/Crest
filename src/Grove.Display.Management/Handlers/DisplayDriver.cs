using Grove.Display.Management.Views;

namespace Grove.Display.Management.Handlers;

public abstract class DisplayDriver<TModel, TDisplayContext> :
    DisplayDriverBase,
    IDisplayDriver<TModel, TDisplayContext>
    where TModel : class
    where TDisplayContext : BuildDisplayContext
{
    protected static readonly string ModelName = typeof(TModel).Name;

    /// <summary>
    /// Returns <see langword="true"/> if the model can be handled by the current driver.
    /// </summary>
    public virtual bool CanHandleModel(TModel model)
        => true;

    Task<IDisplayResult> IDisplayDriver<TModel, TDisplayContext>.BuildDisplayAsync(TModel model, TDisplayContext context)
    {
        if (!CanHandleModel(model))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        BuildPrefix(model, context.HtmlFieldPrefix);

        return DisplayAsync(model, context);
    }

    public virtual Task<IDisplayResult> DisplayAsync(TModel model, TDisplayContext context)
        => Task.FromResult(Display(model, context));

    public virtual IDisplayResult Display(TModel model, TDisplayContext context)
        => NullShapeResult();

    protected static IDisplayResult NullShapeResult()
        => null;

    protected virtual void BuildPrefix(TModel model, string htmlFieldPrefix)
    {
        if (!string.IsNullOrEmpty(htmlFieldPrefix))
        {
            Prefix = $"{htmlFieldPrefix}.{ModelName}";
        }
        else
        {
            Prefix = ModelName;
        }
    }
}

public abstract class DisplayDriver<TModel> :
    DisplayDriver<TModel, BuildDisplayContext>,
    IDisplayDriver<TModel>
    where TModel : class;
