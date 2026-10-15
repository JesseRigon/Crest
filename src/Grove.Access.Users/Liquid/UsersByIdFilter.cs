using Fluid;
using Fluid.Values;
using Grove.Display.Liquid;
using Grove.Access.Users.Indexes;
using Grove.Access.Users.Models;
using YesSql;
using YesSql.Services;

namespace Grove.Access.Users.Liquid;

public class UsersByIdFilter : ILiquidFilter
{
    private readonly ISession _session;
    private readonly Grove.Access.IAccessDecision _decision;
    private readonly Grove.Access.ICallerContextAccessor _callers;

    public UsersByIdFilter(ISession session, Grove.Access.IAccessDecision decision, Grove.Access.ICallerContextAccessor callers)
    {
        _session = session;
        _decision = decision;
        _callers = callers;
    }

    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext ctx)
    {
        if (input.Type == FluidValues.Dictionary)
        {
            if (input.ToObjectValue() is IFluidIndexable values)
            {
                var items = new Dictionary<long, string>();

                foreach (var key in values.Keys)
                {
                    if (long.TryParse(key, out var id) && values.TryGetValue(key, out var value))
                    {
                        items.Add(id, value.ToStringValue());
                    }
                }

                var cachedUsers = await _session.GetAsync<User>(items.Keys.ToArray());

                var cachedUserIds = cachedUsers.Select(x => x.UserId).ToHashSet();

                var missingUserIds = items.Values.Where(userId => !cachedUserIds.Contains(userId)).ToList();

                var missingUsers = await _session.Query<User, UserIndex>(x => x.UserId.IsIn(missingUserIds)).ListAsync();

                return FluidValue.Create(await LiquidUserScope.VisibleAsync(missingUsers.Concat(cachedUsers), _decision, _callers), ctx.Options);
            }

            return EmptyValue.Instance;
        }

        if (input.Type == FluidValues.Array)
        {
            // List of user ids
            var userIds = input.Enumerate(ctx).Select(x => x.ToStringValue()).ToArray();

            return FluidValue.Create(await LiquidUserScope.VisibleAsync(await _session.Query<User, UserIndex>(x => x.UserId.IsIn(userIds)).ListAsync(), _decision, _callers), ctx.Options);
        }

        var userId = input.ToStringValue();

        var user = await _session.Query<User, UserIndex>(x => x.UserId == userId).FirstOrDefaultAsync();
        return user is not null && (await LiquidUserScope.VisibleAsync([user], _decision, _callers)).Count == 1
            ? FluidValue.Create(user, ctx.Options)
            : NilValue.Instance;
    }
}
