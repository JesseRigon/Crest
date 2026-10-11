using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Crest.DisplayManagement.Handlers;
using Crest.Environment.Shell;
using Crest.Modules;
using Crest.Navigation;
using Crest.OpenApi.Drivers;
using Crest.OpenApi.Settings;
using Crest.Security.Permissions;
using Crest.Settings;
using Scalar.AspNetCore;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Crest.OpenApi;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.ShouldInclude = operation => operation.HttpMethod != null;
        });

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "OpenApi V1", Version = "v1" });

            c.DocInclusionPredicate(
                (docName, apiDesc) =>
                {
                    return apiDesc.HttpMethod != null;
                }
            );

            // Without this, operations are emitted in whatever order ASP.NET Core's action
            // discovery happens to enumerate them, which follows assembly/feature load order —
            // not source order. In Crest's modular architecture that load order shifts
            // whenever an unrelated feature is enabled/disabled, reshuffling unrelated methods
            // in the generated NSwag clients on every regeneration. Sorting by path + verb is
            // deterministic regardless of load order, so only actually-changed endpoints move.
            c.OrderActionsBy(apiDesc => $"{apiDesc.RelativePath}_{apiDesc.HttpMethod}");

            // The API is bearer-only. Declaring a single HTTP bearer scheme keeps the generated
            // OpenAPI document honest (operations show as secured) and makes the generated NSwag
            // clients token-aware. The documentation UIs never surface a manual "Authorize" step:
            // they acquire and attach the token silently (see the injected openapi-ui-auth bundle).
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Bearer token acquired silently from the tenant's OpenID Connect server.",
            });

            c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference("Bearer", doc), new List<string>() },
            });
        });

        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddSiteDisplayDriver<OpenApiSettingsDisplayDriver>();
    }

    public override void Configure(
        IApplicationBuilder app,
        IEndpointRouteBuilder routes,
        IServiceProvider serviceProvider
    )
    {
        var shellSettings = app.ApplicationServices.GetRequiredService<ShellSettings>();
        var prefix = shellSettings?.RequestUrlPrefix;

        // Read settings to determine schema-access configuration.
        var siteService = app.ApplicationServices.GetRequiredService<ISiteService>();
        var settings = siteService.GetSettingsAsync<OpenApiSettings>().GetAwaiter().GetResult();

        // Add the server URL to the Swagger document so Scalar knows where to send requests.
        var swaggerGenOptions = app.ApplicationServices.GetRequiredService<
            IOptions<SwaggerGenOptions>
        >();
        var basePath = !string.IsNullOrEmpty(prefix) ? $"/{prefix}" : "/";
        swaggerGenOptions.Value.AddServer(new OpenApiServer { Url = basePath });

        // Protect OpenAPI documentation endpoints with the ViewOpenApiContent permission.
        // JSON spec endpoints can optionally be left open for NSwag / code generators
        // via the AllowAnonymousSchemaAccess setting.
        app.Use(
            async (context, next) =>
            {
                var path = context.Request.Path.Value;

                if (path != null)
                {
                    var isSwaggerJson =
                        path.StartsWith("/swagger/", StringComparison.OrdinalIgnoreCase)
                        && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                    var isOpenApiJson =
                        path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase)
                        && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

                    if (isSwaggerJson || isOpenApiJson)
                    {
                        // JSON spec endpoints — skip auth when anonymous access is allowed.
                        if (!settings.AllowAnonymousSchemaAccess)
                        {
                            var user = context.User;

                            // The request path's gate authenticated the request (the Api
                            // scheme when it carries a bearer token); the principal is the gate's.
                            if (user?.Identity?.IsAuthenticated != true)
                            {
                                // Challenge through the "Api" scheme rather than writing a bare
                                // status code, so the 401 carries a WWW-Authenticate header
                                // (RFC 9110 §15.5.2) and a Problem Details body — matching the
                                // responses of the API endpoints the schema describes.
                                await context.ChallengeAsync(PlatformConstants.AuthenticationSchemes.Api);
                                return;
                            }

                            var authorizationService =
                                context.RequestServices.GetRequiredService<IAuthorizationService>();

                            if (
                                !await authorizationService.AuthorizeAsync(
                                    user,
                                    OpenApiPermissions.ViewOpenApiContent
                                )
                            )
                            {
                                await context.ForbidAsync(PlatformConstants.AuthenticationSchemes.Api);
                                return;
                            }
                        }
                    }
                    else
                    {
                        var isSwaggerUI = path.StartsWith(
                            "/swagger",
                            StringComparison.OrdinalIgnoreCase
                        );
                        var isReDoc = path.StartsWith("/redoc", StringComparison.OrdinalIgnoreCase);
                        var isScalar = path.StartsWith(
                            "/scalar",
                            StringComparison.OrdinalIgnoreCase
                        );

                        if (isSwaggerUI || isReDoc || isScalar)
                        {
                            var user = context.User;

                            if (user?.Identity?.IsAuthenticated != true)
                            {
                                context.Response.Redirect($"{context.Request.PathBase}/admin");
                                return;
                            }

                            var authorizationService =
                                context.RequestServices.GetRequiredService<IAuthorizationService>();

                            if (
                                !await authorizationService.AuthorizeAsync(
                                    user,
                                    OpenApiPermissions.ViewOpenApiContent
                                )
                            )
                            {
                                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                return;
                            }
                        }
                    }
                }

                await next();
            }
        );

        // The OpenAPI JSON and Swagger generator are always registered.
        routes.MapOpenApi();
        app.UseSwagger();
    }

    /// <summary>
    /// Builds the head markup injected into the Swagger and Scalar pages: a single
    /// <c>&lt;script type="module"&gt;</c> tag loading the self-bootstrapping oidc-client-ts
    /// bundle, configured through <c>data-*</c> attributes — no inline scripts, no globals. On
    /// load the bundle silently acquires (and renews) a bearer token against the same-tenant
    /// OpenID Connect server using the admin cookie session, wraps <c>window.fetch</c> to attach
    /// it to API requests, and reflects it in Swagger's Authorize dialog when present. The
    /// authority and redirect URIs are derived in the browser from <c>window.location</c> so
    /// reverse proxies and tenant prefixes resolve correctly.
    /// </summary>
    internal static string BuildAuthHeadContent(string prefix)
    {
        var pathBase = !string.IsNullOrEmpty(prefix) ? $"/{prefix}" : string.Empty;

        static string encode(string value) => HtmlEncoder.Default.Encode(value);

        return $"<script type=\"module\" src=\"{encode($"{pathBase}{OpenApiConstants.AuthScriptPath}")}\""
            + " data-openapi-ui-auth"
            + $" data-path-base=\"{encode(pathBase)}\""
            + $" data-client-id=\"{encode(OpenApiConstants.DocumentationClientId)}\""
            + $" data-scope=\"{encode(OpenApiConstants.DocumentationScopes)}\""
            + $" data-silent-path=\"{encode(OpenApiConstants.SilentCallbackPath)}\""
            + "></script>";
    }
}

[Feature("Crest.OpenApi.SwaggerUI")]
public sealed class SwaggerUIStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IOpenApiUIFeature, SwaggerUIFeature>();

    public override void Configure(
        IApplicationBuilder app,
        IEndpointRouteBuilder routes,
        IServiceProvider serviceProvider
    )
    {
        var shellSettings = app.ApplicationServices.GetRequiredService<ShellSettings>();
        var prefix = shellSettings?.RequestUrlPrefix;

        var swaggerJson = !string.IsNullOrEmpty(prefix)
            ? $"/{prefix}/swagger/v1/swagger.json"
            : "/swagger/v1/swagger.json";

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(swaggerJson, "OpenApi V1");
            options.DocumentTitle = "Crest OpenAPI Documentation";

            // Inject the self-bootstrapping silent-auth bundle. It reflects the silently-acquired
            // token in Swagger's Authorize dialog, which makes Swagger UI attach the bearer token
            // to secured operations natively (and re-apply it on each silent renewal), and its
            // window.fetch wrapper lets a rejected cached token (401) be refreshed and the request
            // retried once — a token manually pasted into the Authorize dialog is left untouched.
            options.HeadContent += Startup.BuildAuthHeadContent(prefix);

            // The interceptor only strips the cookie from API calls so the bearer token (not the
            // ambient admin cookie) is what authenticates them. It must stay synchronous: an async
            // interceptor makes Swagger UI's curl-snippet generator throw in escapeShell. Spec/UI
            // fetches keep the cookie so the auth gate does not redirect them to /admin.
            options.UseRequestInterceptor(
                "(req) => { if (req.url && req.url.includes('/api/')) { req.credentials = 'omit'; } return req; }"
            );
        });
    }
}

[Feature("Crest.OpenApi.ReDocUI")]
public sealed class ReDocUIStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IOpenApiUIFeature, ReDocUIFeature>();

    public override void Configure(
        IApplicationBuilder app,
        IEndpointRouteBuilder routes,
        IServiceProvider serviceProvider
    )
    {
        var shellSettings = app.ApplicationServices.GetRequiredService<ShellSettings>();
        var prefix = shellSettings?.RequestUrlPrefix;

        var swaggerJson = !string.IsNullOrEmpty(prefix)
            ? $"/{prefix}/swagger/v1/swagger.json"
            : "/swagger/v1/swagger.json";

        // ReDoc is read-only reference documentation with no "try it out" surface, so it needs no
        // token acquisition — it only fetches the spec, which the admin cookie already covers.
        app.UseReDoc(options =>
        {
            options.RoutePrefix = "redoc";
            options.SpecUrl = swaggerJson;
            options.DocumentTitle = "Crest OpenAPI Documentation";
        });
    }
}

[Feature("Crest.OpenApi.ScalarUI")]
public sealed class ScalarUIStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IOpenApiUIFeature, ScalarUIFeature>();

    public override void Configure(
        IApplicationBuilder app,
        IEndpointRouteBuilder routes,
        IServiceProvider serviceProvider
    )
    {
        var shellSettings = app.ApplicationServices.GetRequiredService<ShellSettings>();
        var prefix = shellSettings?.RequestUrlPrefix;

        routes.MapScalarApiReference(options =>
        {
            // Scalar's own client script re-derives the tenant's URL prefix from
            // window.location.pathname and prepends it to this route pattern. On a
            // path-prefixed tenant, passing an already-prefixed path here (as the
            // Swagger UI/ReDoc endpoints do) makes the prefix apply twice and the
            // spec 404s, leaving Scalar's sidebar empty. Keep this one app-root-relative.
            options
                .WithOpenApiRoutePattern("/swagger/v1/swagger.json")
                .WithTitle("Crest OpenAPI Documentation")
                // Disable Scalar's default external proxy so all requests (token + API)
                // go directly from the browser to this server.
                .WithProxy("");

            // Inject the self-bootstrapping silent-auth bundle; it wraps window.fetch on load so
            // the bearer token is attached to API calls (spec/UI fetches keep the cookie).
            options.AddHeadContent(Startup.BuildAuthHeadContent(prefix));
        });
    }
}
