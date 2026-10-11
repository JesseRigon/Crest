using Crest.Access;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Environment.Cache.CacheContextProviders;

/// <summary>The <c>user.roles</c> cache context: the request's caller's roles, never the principal's claims.</summary>
public class RolesCacheContextProvider(IHttpContextAccessor httpContextAccessor) : ICacheContextProvider
{
    public Task PopulateContextEntriesAsync(IEnumerable<string> contexts, List<CacheContextEntry> entries)
    {
        if (contexts.Any(ctx => string.Equals(ctx, "user.roles", StringComparison.OrdinalIgnoreCase)))
        {
            var caller = httpContextAccessor.HttpContext?.RequestServices.GetService<ICallerContextAccessor>()?.Current;
            if (caller is { IsAuthenticated: true })
            {
                foreach (var role in caller.Roles)
                {
                    entries.Add(new CacheContextEntry("user.roles", role));
                }
            }
        }

        return Task.CompletedTask;
    }
}
