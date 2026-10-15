using Microsoft.AspNetCore.Http;
using Grove.Display.Rules.Models;

namespace Grove.Display.Rules.Services;

public class IsAnonymousConditionEvaluator : ConditionEvaluator<IsAnonymousCondition>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IsAnonymousConditionEvaluator(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<bool> EvaluateAsync(IsAnonymousCondition condition)
        => _httpContextAccessor.HttpContext.User?.Identity.IsAuthenticated != true ? True : False;
}
