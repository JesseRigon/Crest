using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Security.RateLimits;

/// <summary>
/// The shell's rate limiter: the global limiter <see cref="RateLimiterOptions"/> carries for this shell, owned by
/// the shell's container and disposed with it.
/// </summary>
/// <remarks>
/// A partitioned limiter runs a replenishment timer until it is disposed, and the timer keeps the limiter and its
/// partition callbacks alive. ASP.NET's rate-limiting middleware never disposes its limiters (it is built for the
/// application's lifetime), so with one pipeline per shell every released shell stayed reachable from a running
/// timer, its whole container included. The limiters are disposed here instead, with the shell.
/// </remarks>
public sealed class ShellRateLimiter : IAsyncDisposable
{
    private readonly IOptions<RateLimiterOptions> _options;
    private readonly List<PartitionedRateLimiter<HttpContext>> _owned = [];
    private bool _disposed;

    public ShellRateLimiter(IOptions<RateLimiterOptions> options)
    {
        _options = options;
    }

    /// <summary>The shell's rate-limiter options; built on first use.</summary>
    public RateLimiterOptions Options => _options.Value;

    /// <summary>Takes ownership of a limiter built for this shell: it is disposed with the shell.</summary>
    public TLimiter Own<TLimiter>(TLimiter limiter)
        where TLimiter : PartitionedRateLimiter<HttpContext>
    {
        lock (_owned)
        {
            _owned.Add(limiter);
        }

        return limiter;
    }

    /// <summary>Whether the shell's limiters were disposed.</summary>
    public bool IsDisposed => _disposed;

    public async ValueTask DisposeAsync()
    {
        PartitionedRateLimiter<HttpContext>[] owned;
        lock (_owned)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owned = [.. _owned];
            _owned.Clear();
        }

        foreach (var limiter in owned)
        {
            await limiter.DisposeAsync();
        }
    }
}

/// <summary>
/// Applies the shell's global rate limiter to a request, as ASP.NET's rate-limiting middleware does for its global
/// limiter: a permit is attempted, then waited for (a limiter with a queue), and a request without one is answered
/// with <see cref="RateLimiterOptions.RejectionStatusCode"/> and <see cref="RateLimiterOptions.OnRejected"/>.
/// The limiter comes from the request's services each time, so the middleware holds nothing of the shell.
/// </summary>
internal sealed class ShellRateLimitingMiddleware
{
    private readonly RequestDelegate _next;

    public ShellRateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null)
        {
            await _next(context);
            return;
        }

        var options = context.RequestServices.GetRequiredService<ShellRateLimiter>().Options;
        var limiter = options.GlobalLimiter;
        if (limiter is null)
        {
            await _next(context);
            return;
        }

        RateLimitLease lease;
        try
        {
            lease = limiter.AttemptAcquire(context);
            if (!lease.IsAcquired)
            {
                lease.Dispose();
                lease = await limiter.AcquireAsync(context, permitCount: 1, context.RequestAborted);
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        using (lease)
        {
            if (lease.IsAcquired)
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = options.RejectionStatusCode;
            if (options.OnRejected is { } onRejected)
            {
                await onRejected(new OnRejectedContext { HttpContext = context, Lease = lease }, context.RequestAborted);
            }
        }
    }
}

public static class ShellRateLimiterExtensions
{
    /// <summary>Registers the shell's rate limiter and the rate-limiter options it is built from.</summary>
    public static IServiceCollection AddShellRateLimiter(this IServiceCollection services)
    {
        services.AddRateLimiter();
        services.AddSingleton<ShellRateLimiter>();
        return services;
    }

    /// <summary>Limits the shell's requests with its global rate limiter.</summary>
    public static IApplicationBuilder UseShellRateLimiter(this IApplicationBuilder app)
        => app.UseMiddleware<ShellRateLimitingMiddleware>();
}
