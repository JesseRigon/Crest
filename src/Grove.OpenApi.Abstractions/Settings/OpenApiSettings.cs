using System.ComponentModel.DataAnnotations;

namespace Grove.OpenApi.Settings;

public class OpenApiSettings
{
    public const string GroupId = "openapi";

    /// <summary>
    /// Whether the OpenAPI JSON schema endpoints can be accessed without authentication.
    /// When <c>false</c> (the default), the schema endpoints require the
    /// <c>ViewOpenApiContent</c> permission.
    /// </summary>
    [Display(Name = "Allow anonymous access to the API schema", Description = "When enabled, the JSON schema endpoints can be accessed without authentication. When disabled (the default), they require the View OpenAPI Content permission, and external tools like NSwag must provide authentication.", GroupName = "API Schema Access")]
    public bool AllowAnonymousSchemaAccess { get; set; }
}
