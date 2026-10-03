using Crest.Workflows.Api.Client.Resources.Scripting.Models;
using Crest.Workflows.Api.Client.Shared.Enums;

namespace Crest.Workflows.Api.Client.Shared.Models;

public class LogPersistenceConfiguration
{
    public LogPersistenceEvaluationMode EvaluationMode { get; set; }
    public string? StrategyType { get; set; }
    public Expression? Expression { get; set; }
}