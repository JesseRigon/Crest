using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Identity;
using Crest.Liquid;
using Crest.Users.Indexes;
using Crest.Users.Models;
using YesSql;
using YesSql.Services;

namespace Crest.Users.Liquid;

public class UsersByNameFilter : ILiquidFilter
{
    private readonly ISession _session;
    private readonly UserManager<IUser> _userManager;

    public UsersByNameFilter(ISession session, UserManager<IUser> userManager)
    {
        _session = session;
        _userManager = userManager;
    }

    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext ctx)
    {
        if (input.Type == FluidValues.Array)
        {
            // List of usernames
            var normalizedUserNames = input.Enumerate(ctx)
                .Select(x => _userManager.NormalizeName(x.ToStringValue()))
                .ToArray();

            return FluidValue.Create(await _session.Query<User, UserIndex>(x => x.NormalizedUserName.IsIn(normalizedUserNames)).ListAsync(), ctx.Options);
        }

        var userName = input.ToStringValue();
        var normalizedUserName = _userManager.NormalizeName(userName);

        return FluidValue.Create(await _session.Query<User, UserIndex>(x => x.NormalizedUserName == normalizedUserName).FirstOrDefaultAsync(), ctx.Options);
    }
}
