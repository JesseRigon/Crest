using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Identity;
using Grove.Display.Liquid;
using Grove.Access.Users.Indexes;
using Grove.Access.Users.Models;
using YesSql;
using YesSql.Services;

namespace Grove.Access.Users.Liquid;

public class UsersByNameFilter : ILiquidFilter
{
    private readonly ISession _session;
    private readonly UserManager<IUser> _userManager;
    private readonly Grove.Access.IAccessDecision _decision;
    private readonly Grove.Access.ICallerContextAccessor _callers;

    public UsersByNameFilter(ISession session, UserManager<IUser> userManager, Grove.Access.IAccessDecision decision, Grove.Access.ICallerContextAccessor callers)
    {
        _session = session;
        _userManager = userManager;
        _decision = decision;
        _callers = callers;
    }

    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext ctx)
    {
        if (input.Type == FluidValues.Array)
        {
            // List of usernames
            var normalizedUserNames = input.Enumerate(ctx)
                .Select(x => _userManager.NormalizeName(x.ToStringValue()))
                .ToArray();

            return FluidValue.Create(await LiquidUserScope.VisibleAsync(await _session.Query<User, UserIndex>(x => x.NormalizedUserName.IsIn(normalizedUserNames)).ListAsync(), _decision, _callers), ctx.Options);
        }

        var userName = input.ToStringValue();
        var normalizedUserName = _userManager.NormalizeName(userName);

        var user = await _session.Query<User, UserIndex>(x => x.NormalizedUserName == normalizedUserName).FirstOrDefaultAsync();
        return user is not null && (await LiquidUserScope.VisibleAsync([user], _decision, _callers)).Count == 1
            ? FluidValue.Create(user, ctx.Options)
            : NilValue.Instance;
    }
}
