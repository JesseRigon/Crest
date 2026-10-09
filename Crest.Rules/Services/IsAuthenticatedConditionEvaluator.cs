using Microsoft.AspNetCore.Http;
using Crest.Rules.Models;

namespace Crest.Rules.Services;

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
