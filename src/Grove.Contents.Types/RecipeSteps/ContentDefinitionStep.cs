using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Records;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;

namespace Grove.Contents.Types.RecipeSteps;

/// <summary>
/// This recipe step creates content definitions.
/// </summary>
public sealed class ContentDefinitionStep : NamedRecipeStepHandler
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    public ContentDefinitionStep(
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<ContentDefinitionStep> stringLocalizer)
         : base("ContentDefinition")
    {
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var step = context.Step.ToObject<ContentDefinitionStepModel>();

        foreach (var contentType in step.ContentTypes)
        {
            var newType = await _contentDefinitionManager.LoadTypeDefinitionAsync(contentType.Name)
                ?? new ContentTypeDefinition(contentType.Name, contentType.DisplayName);

            await UpdateContentTypeAsync(newType, contentType, context);
        }

        foreach (var contentFieldSet in step.ContentFieldSets)
        {
            var newPart = await _contentDefinitionManager.LoadFieldSetDefinitionAsync(contentFieldSet.Name)
                ?? new ContentFieldSetDefinition(contentFieldSet.Name);

            await UpdateContentFieldSetAsync(newPart, contentFieldSet, context);
        }
    }

    private Task UpdateContentTypeAsync(ContentTypeDefinition type, ContentTypeDefinitionRecord record, RecipeExecutionContext context)
    {
        return _contentDefinitionManager.AlterTypeDefinitionAsync(type.Name, builder =>
        {
            if (!string.IsNullOrEmpty(record.DisplayName))
            {
                builder.WithDisplayName(record.DisplayName);
                builder.MergeSettings(record.Settings);
            }

            foreach (var part in record.ContentTypeFieldSetDefinitionRecords)
            {
                if (string.IsNullOrEmpty(part.FieldSetName))
                {
                    context.Errors.Add(S["Unable to add content-part to the '{0}' content-type. The part name cannot be null or empty.", type.Name]);

                    continue;
                }

                builder.WithFieldSet(part.Name, part.FieldSetName, partBuilder => partBuilder.MergeSettings(part.Settings));
            }

            foreach (var embedding in record.EmbeddedContentDefinitionRecords ?? [])
            {
                builder.WithEmbedding(embedding.Name, embeddingBuilder => embeddingBuilder.From(embedding));
            }
        });
    }

    private Task UpdateContentFieldSetAsync(ContentFieldSetDefinition part, ContentFieldSetDefinitionRecord record, RecipeExecutionContext context)
    {
        return _contentDefinitionManager.AlterFieldSetDefinitionAsync(part.Name, builder =>
        {
            builder.MergeSettings(record.Settings);

            foreach (var field in record.ContentFieldDefinitionRecords)
            {
                if (string.IsNullOrEmpty(field.Name))
                {
                    context.Errors.Add(S["Unable to add content-field to the '{0}' content-part. The part name cannot be null or empty.", part.Name]);

                    continue;
                }

                builder.WithField(field.Name, fieldBuilder =>
                {
                    fieldBuilder.OfType(field.FieldName);
                    fieldBuilder.MergeSettings(field.Settings);
                });
            }
        });
    }

    private sealed class ContentDefinitionStepModel
    {
        public ContentTypeDefinitionRecord[] ContentTypes { get; set; } = [];

        public ContentFieldSetDefinitionRecord[] ContentFieldSets { get; set; } = [];
    }
}
