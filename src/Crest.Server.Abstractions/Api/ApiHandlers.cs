using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Api;

/// <summary>
/// A group of request handlers behind a route group (docs/MVC/api.md › Hosting): the request's
/// services come in by constructor, the request itself through <see cref="HttpContext"/>, and the
/// results keep the bodies the APIs have always answered with (a string is a <c>text/plain</c>
/// body, anything else JSON). A contributor maps each route to a handler method:
/// <code>group.MapGet("{id}", (HttpContext http, [FromRoute] string id) => ApiHandlers.For&lt;ThingHandlers&gt;(http).GetAsync(id));</code>
/// Every handler parameter names its binding source (docs/MVC/api.md › Hosting).
/// </summary>
public abstract class ApiHandlers
{
    private static readonly ConcurrentDictionary<Type, ObjectFactory> s_factories = new();

    private HttpContext? _httpContext;

    /// <summary>Creates the handlers for this request from the request's services.</summary>
    public static T For<T>(HttpContext httpContext)
        where T : ApiHandlers
    {
        var factory = s_factories.GetOrAdd(typeof(T), type => ActivatorUtilities.CreateFactory(type, Type.EmptyTypes));
        var handlers = (T)factory(httpContext.RequestServices, null);
        handlers._httpContext = httpContext;
        return handlers;
    }

    /// <summary>
    /// A route handler that creates the handlers for the request and runs one of their methods.
    /// Use this, never a bare <c>(HttpContext http) => For&lt;T&gt;(http).Method()</c> lambda: a
    /// lambda whose only parameter is the HttpContext binds to the <c>RequestDelegate</c>
    /// overload of MapGet/MapPost, which awaits the task and discards the <see cref="IResult"/>,
    /// answering 200 with an empty body. The returned delegate goes through the request
    /// delegate factory, which executes the result.
    /// </summary>
    public static Delegate Handle<THandlers>(Func<THandlers, Task<IResult>> handle)
        where THandlers : ApiHandlers
    {
        Func<HttpContext, Task<IResult>> handler = httpContext => handle(For<THandlers>(httpContext));
        return handler;
    }

    /// <summary>The synchronous form of <see cref="Handle{THandlers}(Func{THandlers, Task{IResult}})"/>.</summary>
    public static Delegate Handle<THandlers>(Func<THandlers, IResult> handle)
        where THandlers : ApiHandlers
    {
        Func<HttpContext, IResult> handler = httpContext => handle(For<THandlers>(httpContext));
        return handler;
    }

    /// <summary>Binds handlers constructed by hand (a test) to the request they answer.</summary>
    public void BindTo(HttpContext httpContext) => _httpContext = httpContext;

    public HttpContext HttpContext => _httpContext ?? throw new InvalidOperationException("The handlers were not created for a request.");

    public HttpRequest Request => HttpContext.Request;

    public HttpResponse Response => HttpContext.Response;

    public ClaimsPrincipal User => HttpContext.User;

    protected static IResult Ok() => TypedResults.Ok();

    protected static IResult Ok(object? value) => value is string text ? TypedResults.Text(text) : TypedResults.Json(value);

    protected static IResult NoContent() => TypedResults.NoContent();

    protected static IResult Forbid() => TypedResults.Forbid();

    protected static IResult Unauthorized() => TypedResults.Unauthorized();

    protected static IResult Unauthorized(object? value) => Body(StatusCodes.Status401Unauthorized, value);

    protected static IResult NotFound() => TypedResults.NotFound();

    protected static IResult NotFound(object? value) => Body(StatusCodes.Status404NotFound, value);

    protected static IResult BadRequest() => TypedResults.BadRequest();

    protected static IResult BadRequest(object? value) => Body(StatusCodes.Status400BadRequest, value);

    protected static IResult Conflict() => TypedResults.Conflict();

    protected static IResult Conflict(object? value) => Body(StatusCodes.Status409Conflict, value);

    protected static IResult StatusCode(int statusCode) => TypedResults.StatusCode(statusCode);

    protected static IResult StatusCode(int statusCode, object? value) => Body(statusCode, value);

    protected static IResult Created(string uri, object? value) => TypedResults.Created(uri, value);

    protected static IResult Problem(string? detail = null, string? instance = null, int? statusCode = null, string? title = null, string? type = null) =>
        TypedResults.Problem(detail, instance, statusCode, title, type);

    protected static IResult Content(string content, string contentType) => TypedResults.Content(content, contentType);

    protected static IResult File(byte[] contents, string contentType, string? fileDownloadName = null) =>
        TypedResults.File(contents, contentType, fileDownloadName);

    protected static IResult File(Stream contents, string contentType, string? fileDownloadName = null) =>
        TypedResults.File(contents, contentType, fileDownloadName);

    private static IResult Body(int statusCode, object? value) => value switch
    {
        null => TypedResults.StatusCode(statusCode),
        string text => TypedResults.Text(text, statusCode: statusCode),
        _ => TypedResults.Json(value, statusCode: statusCode),
    };
}
