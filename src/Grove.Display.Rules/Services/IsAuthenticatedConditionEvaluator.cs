using Microsoft.AspNetCore.Http;
using Grove.Display.Rules.Models;

namespace Grove.Display.Rules.Services;

public class IsAuthenticatedConditionEvaluator : ConditionEvaluator<IsAuthenticatedCondition>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IsAuthenticatedConditionEvaluator(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<bool> EvaluateAsync(IsAuthenticatedCondition condition)
        => _httpContextAccessor.HttpContext.User?.Identity.IsAuthenticated == true ? True : False;
}
