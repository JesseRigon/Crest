using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Crest.Workflows.Common.Converters;
using Crest.Workflows.Expressions.Contracts;
using Crest.Workflows.Expressions.Helpers;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Extensions;
using JetBrains.Annotations;

namespace Crest.Workflows.Expressions;

/// <summary>
/// Evaluates an object expression.
/// </summary>
[UsedImplicitly]
public class ObjectExpressionHandler : IExpressionHandler
{
    private JsonSerializerOptions? _serializerOptions;

    private JsonSerializerOptions SerializerOptions =>
        _serializerOptions ??= new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReferenceHandler = ReferenceHandler.Preserve,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        }.WithConverters(
            new IntegerJsonConverter(),
            new DecimalJsonConverter(),
            new JsonStringEnumConverter());

    /// <inheritdoc />
    [RequiresUnreferencedCode("The type is not known at compile time.")]
    public ValueTask<object?> EvaluateAsync(Expression expression, Type returnType, ExpressionExecutionContext context, ExpressionEvaluatorOptions options)
    {
        var value = expression.Value.ConvertTo<string>() ?? "";

        if (string.IsNullOrWhiteSpace(value))
            return ValueTask.FromResult(default(object?));

        var converterOptions = new ObjectConverterOptions(SerializerOptions);
        var model = value.ConvertTo(returnType, converterOptions);
        return ValueTask.FromResult(model);
    }
}