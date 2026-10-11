using Fluid;
using Fluid.Values;
using Crest.Access;
using Crest.Liquid;

namespace Crest.Users.Liquid;

/// <summary>Whether the request's caller holds a role; reads the one caller, never the principal.</summary>
public class IsInRoleFilter(ICallerContextAccessor callers) : ILiquidFilter
{
    public ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext ctx)
    {
        if (input.ToObjectValue() is LiquidUserAccessor && callers.Current is { } caller)
        {
            var roleName = arguments["name"].Or(arguments.At(0)).ToStringValue();
            if (caller.Roles.Contains(roleName))
            {
                return ValueTask.FromResult<FluidValue>(BooleanValue.True);
            }
        }

        return ValueTask.FromResult<FluidValue>(BooleanValue.False);
    }
}
