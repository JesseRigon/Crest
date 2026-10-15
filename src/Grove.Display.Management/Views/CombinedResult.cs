using Grove.Display.Management.Handlers;

namespace Grove.Display.Management.Views;

public class CombinedResult : IDisplayResult
{
    private readonly IEnumerable<IDisplayResult> _results;

    public CombinedResult(params IDisplayResult[] results)
    {
        _results = results ?? [];
    }

    public CombinedResult(IEnumerable<IDisplayResult> results)
    {
        _results = results ?? [];
    }

    public async Task ApplyAsync(BuildDisplayContext context)
    {
        foreach (var result in _results)
        {
            await result.ApplyAsync(context);
        }
    }

    public IEnumerable<IDisplayResult> GetResults()
    {
        return _results;
    }
}
