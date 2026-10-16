using System.ComponentModel.DataAnnotations;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.Handlers;

public class ValidateContentFieldSetContext : ValidateContentContext
{
    public ValidateContentFieldSetContext(ContentItem contentItem)
        : base(contentItem)
    {
    }

    public ContentTypeFieldSetDefinition ContentTypeFieldSetDefinition { get; set; }
}

public static class ValidateContentFieldSetContextExtensions
{
    public static void Fail(this ValidateContentFieldSetContext context, ValidationResult error)
    {
        context.ContentValidateResult.Fail(error);
    }

    public static void Fail(this ValidateContentFieldSetContext context, string errorMessage, string propertyName)
    {
        if (propertyName == null)
        {
            context.ContentValidateResult.Fail(new ValidationResult(errorMessage));
        }
        else
        {
            var memberName = $"{context.ContentTypeFieldSetDefinition.Name}.{propertyName}";

            context.ContentValidateResult.Fail(new ValidationResult(errorMessage, new[] { memberName }));
        }
    }
}
