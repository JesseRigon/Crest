using Grove.Workflows.Expressions.Contracts;
using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Models;

namespace Grove.Workflows;

public record ActivityInputEvaluatorContext(
    ActivityExecutionContext ActivityExecutionContext, 
    ExpressionExecutionContext ExpressionExecutionContext, 
    InputDescriptor InputDescriptor, 
    Input Input, 
    IExpressionEvaluator ExpressionEvaluator);