using System.ComponentModel.DataAnnotations;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.Handlers;

public class ValidateContentPartContext : ValidateContentContext
{
    public ValidateContentPartContext(ContentItem contentItem)
        : base(contentItem)
    {
    }

    public ContentTypePartDefinition ContentTypePartDefinition { get; set; }
}

public static class ValidateContentPartContextExtensions
{
    public static void Fail(this ValidateContentPartContext context, ValidationResult error)
    {
        context.ContentValidateResult.Fail(error);
    }

    public static void Fail(this ValidateContentPartContext context, string errorMessage, string propertyName)
    {
        if (propertyName == null)
        {
            context.ContentValidateResult.Fail(new ValidationResult(errorMessage));
        }
        else
        {
            var memberName = $"{context.ContentTypePartDefinition.Name}.{propertyName}";

            context.ContentValidateResult.Fail(new ValidationResult(errorMessage, new[] { memberName }));
        }
    }
}
