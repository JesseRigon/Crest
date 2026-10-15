using Microsoft.AspNetCore.Builder;

namespace Grove.Api;

/// <summary>
/// Endpoint metadata: the endpoint answers the request's validation sink (docs/MVC/api.md › The
/// error contract, E3). When its handler succeeds while the content manager or a part or field
/// handler raised errors into the request's <see cref="Grove.Validation.IValidationSink"/>, the
/// registry's route group answers the error contract's 400 with the sink's <c>errors</c> map instead.
/// A handler can also answer the sink itself with <see cref="ApiProblems.Validation(Grove.Validation.IValidationSink)"/>.
/// </summary>
public sealed class ValidationSinkMetadata
{
    public static readonly ValidationSinkMetadata Instance = new();
}

public static class ValidationSinkEndpointConventionExtensions
{
    /// <summary>Answers the request's validation sink errors as the error contract's 400 (<see cref="ValidationSinkMetadata"/>).</summary>
    public static TBuilder AnswersValidationSink<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpoint => endpoint.Metadata.Add(ValidationSinkMetadata.Instance));
        return builder;
    }
}
