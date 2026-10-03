using System.Text.Json;
using System.Text.Json.Serialization;
using Crest.Workflows.Expressions.Contracts;
using Crest.Workflows.Expressions.Models;

namespace Crest.Workflows.Serialization.Converters;

/// <summary>
/// A JSON converter factory that creates <see cref="ExpressionJsonConverter"/> instances.
/// </summary>
public class ExpressionJsonConverterFactory : JsonConverterFactory
{
    private readonly IExpressionDescriptorRegistry _expressionDescriptorRegistry;

    /// <inheritdoc />
    public ExpressionJsonConverterFactory(IExpressionDescriptorRegistry expressionDescriptorRegistry)
    {
        _expressionDescriptorRegistry = expressionDescriptorRegistry;
    }

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeof(Expression).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return new ExpressionJsonConverter(_expressionDescriptorRegistry);
    }
}