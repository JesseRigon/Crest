using Crest.Workflows.Expressions.Contracts;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Models;

namespace Crest.Workflows;

public record ActivityInputEvaluatorContext(
    ActivityExecutionContext ActivityExecutionContext, 
    ExpressionExecutionContext ExpressionExecutionContext, 
    InputDescriptor InputDescriptor, 
    Input Input, 
    IExpressionEvaluator ExpressionEvaluator);