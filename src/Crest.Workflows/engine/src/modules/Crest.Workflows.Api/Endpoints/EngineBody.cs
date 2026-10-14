using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Crest.Api;
using Crest.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Api.Endpoints;

/// <summary>
/// A handler parameter bound from the request's JSON body through the engine's
/// <see cref="IApiSerializer"/> (the polymorphic activity JSON, expando inputs and the engine's
/// converters), which the host's JSON options do not know. The one <c>BindAsync</c> source of
/// the engine API: minimal APIs call <see cref="BindAsync"/> for a parameter of this type. An
/// empty body, or one without a JSON content type, binds a new <typeparamref name="T"/>; a body
/// that is not valid JSON binds an <see cref="Error"/>, which <see cref="EngineBodyFilter"/> on the
/// engine's route group answers with the error contract's 400 before the handler runs. The
/// registry's validation (api.md › Validation) sees the wrapper as the argument: it is an
/// <see cref="IValidatableObject"/> that runs the body's own rules (<see cref="ObjectRules"/>)
/// under the body's wire-name keys, and <see cref="Value"/> is a field, not a property, so the
/// reflection resolver does not validate the body a second time under a <c>value.</c> prefix.
/// </summary>
public sealed class EngineBody<T> : IEngineBody, IValidatableObject
    where T : class, new()
{
    /// <summary>The deserialized body, or a new instance when the request carried none.</summary>
    public readonly T Value;

    private EngineBody(T value, string? error)
    {
        Value = value;
        Error = error;
    }

    /// <inheritdoc />
    public string? Error { get; }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();
        if (Error is null)
        {
            ObjectRules.Validate(Value, validationContext, string.Empty, results);
        }

        return results;
    }

    public static async ValueTask<EngineBody<T>?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var request = context.Request;
        if (request.ContentLength == 0 || !request.HasJsonContentType())
        {
            return new(new T(), null);
        }

        // Read first: a JSON content type with an empty body (a POST with nothing to say) is a
        // new T, as the engine's clients expect of an optional body.
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, context.RequestAborted);
        var bytes = buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
        ReadOnlySpan<byte> whitespace = [(byte)' ', (byte)'\n', (byte)'\r', (byte)'\t'];
        if (bytes.Span.IndexOfAnyExcept(whitespace) < 0)
        {
            return new(new T(), null);
        }

        var options = context.RequestServices.GetRequiredService<IApiSerializer>().GetOptions();
        try
        {
            var value = JsonSerializer.Deserialize<T>(bytes.Span, options);
            return new(value ?? new T(), null);
        }
        catch (JsonException exception)
        {
            return new(new T(), exception.Message);
        }
    }
}

/// <summary>What <see cref="EngineBodyFilter"/> inspects on a bound body.</summary>
public interface IEngineBody
{
    /// <summary>Why the body could not be deserialized, or null when it was.</summary>
    string? Error { get; }
}

/// <summary>Answers a body that is not valid JSON with the error contract's 400.</summary>
public sealed class EngineBodyFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is IEngineBody { Error: { } error })
            {
                return ValueTask.FromResult<object?>(ApiProblems.Validation(EngineResults.GeneralErrorKey, error));
            }
        }

        return next(context);
    }
}
