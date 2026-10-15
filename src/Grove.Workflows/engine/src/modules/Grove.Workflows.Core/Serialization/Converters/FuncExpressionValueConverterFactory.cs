using System.Text.Json;
using System.Text.Json.Serialization;
using Grove.Workflows.Expressions.Models;

namespace Grove.Workflows.Serialization.Converters;

public class FuncExpressionConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type t) => t == typeof(Func<ExpressionExecutionContext, ValueTask<object>>);

    public override JsonConverter CreateConverter(Type t, JsonSerializerOptions opts) => new FuncExpressionValueConverter();
}
