using Grove.Workflows.Api.Client.Resources.Scripting.Models;
using Grove.Workflows.Api.Client.Shared.Enums;

namespace Grove.Workflows.Api.Client.Shared.Models;

public class LogPersistenceConfiguration
{
    public LogPersistenceEvaluationMode EvaluationMode { get; set; }
    public string? StrategyType { get; set; }
    public Expression? Expression { get; set; }
}