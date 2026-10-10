namespace Crest.Access.Services;

public sealed class AccessRunner(ICallerContextAccessor accessor) : IAccessRunner
{
    public async Task RunAsAsync(CallerContext caller, Func<Task> work)
    {
        var previous = accessor.Current;
        accessor.Current = caller;
        try
        {
            await work();
        }
        finally
        {
            accessor.Current = previous;
        }
    }

    public async Task<T> RunAsAsync<T>(CallerContext caller, Func<Task<T>> work)
    {
        var previous = accessor.Current;
        accessor.Current = caller;
        try
        {
            return await work();
        }
        finally
        {
            accessor.Current = previous;
        }
    }
}
