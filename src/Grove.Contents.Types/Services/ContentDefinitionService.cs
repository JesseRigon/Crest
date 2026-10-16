using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Contents.Types.Events;
using Grove.Modules;
using Grove.Utilities;

namespace Grove.Contents.Types.Services;

public class ContentDefinitionService : IContentDefinitionService
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IEnumerable<IContentDefinitionEventHandler> _contentDefinitionEventHandlers;
    private readonly ContentTypesOptions _contentTypesOptions;
    private readonly ContentOptions _contentOptions;
    private readonly ILogger _logger;
    protected readonly IStringLocalizer S;

    public ContentDefinitionService(
            IContentDefinitionManager contentDefinitionManager,
            IEnumerable<IContentDefinitionEventHandler> contentDefinitionEventHandlers,
            IEnumerable<ContentFieldSet> contentFieldSets,
            IEnumerable<ContentField> contentFields,
            IOptions<ContentTypesOptions> contentTypesOptions,
            IOptions<ContentOptions> contentOptions,
            ILogger<ContentDefinitionService> logger,
            IStringLocalizer<ContentDefinitionService> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentDefinitionEventHandlers = contentDefinitionEventHandlers;
        _contentTypesOptions = contentTypesOptions.Value;
        _contentOptions = contentOptions.Value;

        foreach (var element in contentFieldSets.Select(x => x.GetType()))
        {
            logger.LogError("The field set '{ContentFieldSet}' should not be registered in DI. Use AddContentFieldSet<T>() instead.", element);
        }

        foreach (var element in contentFields.Select(x => x.GetType()))
        {
            logger.LogError("The content field '{ContentField}' should not be registered in DI. Use AddContentField<T>() instead.", element);
        }

        _logger = logger;
        S = stringLocalizer;
    }

    public async Task<ContentTypeDefinition> AddTypeAsync(string name, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException($"The '{nameof(displayName)}' can't be null or empty.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = await GenerateContentTypeNameFromDisplayNameAsync(displayName);
        }
        else
        {
            if (!char.IsAsciiLetter(name[0]))
            {
                throw new ArgumentException("Content type name must start with a letter", nameof(name));
            }
            if (!string.Equals(name, name.ToSafeName(), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Content type name contains invalid characters", nameof(name));
            }
        }

        while (await _contentDefinitionManager.LoadTypeDefinitionAsync(name) is not null)
        {
            name = VersionName(name);
        }

        var contentTypeDefinition = new ContentTypeDefinition(name, displayName);

        await _contentDefinitionManager.StoreTypeDefinitionAsync(contentTypeDefinition);

        // Ensure it has its own part.
        await _contentDefinitionManager.AlterTypeDefinitionAsync(name, builder => builder.WithFieldSet(name));
        await _contentDefinitionManager.AlterTypeDefinitionAsync(name, cfg =>
        {
            cfg.Creatable()
                .Draftable()
                .Versionable()
                .Listable()
                .Securable()
                .WithThumbnailPath(_contentTypesOptions.DefaultThumbnailPath)
                .WithCategory(_contentTypesOptions.DefaultCategory);
        });

        var context = new ContentTypeCreatedContext
        {
            ContentTypeDefinition = contentTypeDefinition,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentTypeCreated(ctx), context, _logger);

        return contentTypeDefinition;
    }

    public async Task RemoveTypeAsync(string name, bool deleteContent)
    {
        // First remove all attached parts.
        var typeDefinition = await _contentDefinitionManager.LoadTypeDefinitionAsync(name);

        if (typeDefinition == null)
        {
            return;
        }

        var settings = typeDefinition.GetSettings<ContentSettings>();

        if (settings.IsSystemDefined)
        {
            throw new InvalidOperationException("Unable to remove system-defined type.");
        }

        var partDefinitions = typeDefinition.FieldSets.ToList();
        foreach (var partDefinition in partDefinitions)
        {
            await RemoveFieldSetFromTypeAsync(partDefinition.FieldSetDefinition.Name, name);
        }

        // The type's own field set goes with it unless code defines it or another type attaches it.
        if (partDefinitions.Any(part => part.FieldSetDefinition.Name == name) &&
            await TypeOwnFieldSet.CanDeleteAsync(_contentDefinitionManager, _contentOptions, name))
        {
            await RemoveFieldSetAsync(name);
        }

        await _contentDefinitionManager.DeleteTypeDefinitionAsync(name);

        var context = new ContentTypeRemovedContext
        {
            ContentTypeDefinition = typeDefinition,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentTypeRemoved(ctx), context, _logger);
    }

    public async Task AddFieldSetToTypeAsync(string fieldSetName, string typeName)
    {
        var typeDefinition = await _contentDefinitionManager.LoadTypeDefinitionAsync(typeName);
        var position = GetFieldSetPosition(typeDefinition, fieldSetName);

        await _contentDefinitionManager.AlterTypeDefinitionAsync(typeName, typeBuilder => typeBuilder.WithFieldSet(fieldSetName, partBuilder => partBuilder.WithPosition(position)));
        var context = new ContentFieldSetAttachedContext
        {
            ContentTypeName = typeName,
            ContentFieldSetName = fieldSetName,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentFieldSetAttached(ctx), context, _logger);
    }

    public async Task AddReusableFieldSetToTypeAsync(string name, string displayName, string description, string fieldSetName, string typeName)
    {
        var typeDefinition = await _contentDefinitionManager.LoadTypeDefinitionAsync(typeName);
        var position = GetFieldSetPosition(typeDefinition, name);

        await _contentDefinitionManager.AlterTypeDefinitionAsync(typeName, typeBuilder => typeBuilder
            .WithFieldSet(name, fieldSetName, partBuilder =>
            {
                partBuilder.WithDisplayName(displayName);
                partBuilder.WithDescription(description);
                partBuilder.WithPosition(position);
            })
        );

        var context = new ContentFieldSetAttachedContext
        {
            ContentTypeName = typeName,
            ContentFieldSetName = fieldSetName,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentFieldSetAttached(ctx), context, _logger);
    }

    public async Task RemoveFieldSetFromTypeAsync(string fieldSetName, string typeName)
    {
        var typeDefinition = await _contentDefinitionManager.LoadTypeDefinitionAsync(typeName);

        if (typeDefinition == null)
        {
            return;
        }

        var partDefinition = typeDefinition.FieldSets.FirstOrDefault(p => string.Equals(p.Name, fieldSetName, StringComparison.OrdinalIgnoreCase));

        if (partDefinition == null)
        {
            return;
        }

        var settings = partDefinition.GetSettings<ContentSettings>();

        if (settings.IsSystemDefined)
        {
            throw new InvalidOperationException("Unable to remove system-defined part.");
        }

        await _contentDefinitionManager.AlterTypeDefinitionAsync(typeName, typeBuilder => typeBuilder.RemoveFieldSet(fieldSetName));

        var context = new ContentFieldSetDetachedContext
        {
            ContentTypeName = typeName,
            ContentFieldSetName = fieldSetName,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentFieldSetDetached(ctx), context, _logger);
    }

    public async Task<ContentFieldSetDefinition> AddFieldSetAsync(string name)
    {
        if (await _contentDefinitionManager.LoadFieldSetDefinitionAsync(name) is not null)
        {
            throw new Exception(S["Cannot add part named '{0}'. It already exists.", name]);
        }

        if (!string.IsNullOrEmpty(name))
        {
            await _contentDefinitionManager.AlterFieldSetDefinitionAsync(name, builder => builder.Attachable());
            var partDefinition = await _contentDefinitionManager.LoadFieldSetDefinitionAsync(name);

            var context = new ContentFieldSetCreatedContext
            {
                ContentFieldSetDefinition = partDefinition,
            };

            _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentFieldSetCreated(ctx), context, _logger);

            return partDefinition;
        }

        return null;
    }

    public async Task RemoveFieldSetAsync(string name)
    {
        var partDefinition = await _contentDefinitionManager.LoadFieldSetDefinitionAsync(name);

        if (partDefinition == null)
        {
            // Couldn't find this named part, ignore it.
            return;
        }

        var settings = partDefinition.GetSettings<ContentSettings>();

        if (settings.IsSystemDefined)
        {
            throw new InvalidOperationException("Unable to remove system-defined part.");
        }

        foreach (var fieldDefinition in partDefinition.Fields)
        {
            await RemoveFieldFromFieldSetAsync(fieldDefinition.Name, name);
        }

        await _contentDefinitionManager.DeleteFieldSetDefinitionAsync(name);

        var context = new ContentFieldSetRemovedContext
        {
            ContentFieldSetDefinition = partDefinition,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentFieldSetRemoved(ctx), context, _logger);
    }

    public Task AddFieldToFieldSetAsync(string fieldName, string fieldTypeName, string fieldSetName)
        => AddFieldToFieldSetAsync(fieldName, fieldName, fieldTypeName, fieldSetName);

    public async Task AddFieldToFieldSetAsync(string fieldName, string displayName, string fieldTypeName, string fieldSetName)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            throw new ArgumentException($"The '{nameof(fieldName)}' can't be null or empty.", nameof(fieldName));
        }

        var partDefinition = await _contentDefinitionManager.LoadFieldSetDefinitionAsync(fieldSetName);
        var typeDefinition = await _contentDefinitionManager.LoadTypeDefinitionAsync(fieldSetName);

        // If the type exists ensure it has its own part.
        if (typeDefinition != null)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(fieldSetName, builder => builder.WithFieldSet(fieldSetName));
        }

        fieldName = fieldName.ToSafeName();
        var position = GetFieldPosition(partDefinition, fieldName);

        await _contentDefinitionManager.AlterFieldSetDefinitionAsync(fieldSetName,
            partBuilder => partBuilder.WithField(fieldName, fieldBuilder => fieldBuilder.OfType(fieldTypeName).WithDisplayName(displayName).MergeSettings<ContentFieldSettings>(x => x.Position = position)));

        _contentDefinitionEventHandlers.Invoke((handler, context) => handler.ContentFieldAttached(context), new ContentFieldAttachedContext
        {
            ContentFieldSetName = fieldSetName,
            ContentFieldTypeName = fieldTypeName,
            ContentFieldName = fieldName,
            ContentFieldDisplayName = displayName,
        }, _logger);
    }

    public async Task RemoveFieldFromFieldSetAsync(string fieldName, string fieldSetName)
    {
        var partDefinition = await _contentDefinitionManager.LoadFieldSetDefinitionAsync(fieldSetName);

        if (partDefinition == null)
        {
            return;
        }

        var settings = partDefinition.GetSettings<ContentSettings>();

        if (settings.IsSystemDefined)
        {
            throw new InvalidOperationException("Unable to remove system-defined field.");
        }

        await _contentDefinitionManager.AlterFieldSetDefinitionAsync(fieldSetName, typeBuilder => typeBuilder.RemoveField(fieldName));

        var context = new ContentFieldDetachedContext
        {
            ContentFieldSetName = fieldSetName,
            ContentFieldName = fieldName,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentFieldDetached(ctx), context, _logger);
    }

    public async Task AlterFieldAsync(AlterFieldContext context)
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync(context.FieldSetName, partBuilder =>
        {
            partBuilder.WithField(context.FieldName, fieldBuilder =>
            {
                fieldBuilder.WithDisplayName(context.DisplayName);
                fieldBuilder.WithEditor(context.Editor);
                fieldBuilder.WithDisplayMode(context.DisplayMode);
            });
        });

        var eventContext = new ContentFieldUpdatedContext
        {
            ContentFieldSetName = context.FieldSetName,
            ContentFieldName = context.FieldName,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentFieldUpdated(ctx), eventContext, _logger);
    }

    public async Task AlterTypeFieldSetAsync(AlterTypeFieldSetContext context)
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync(context.TypeName, type =>
        {
            type.WithFieldSet(context.FieldSetName, context.FieldSetDefinition, part =>
            {
                part.WithDisplayName(context.DisplayName);
                part.WithDescription(context.Description);
                part.WithEditor(context.Editor);
                part.WithDisplayMode(context.DisplayMode);
            });
        });

        var eventContext = new ContentTypeFieldSetUpdatedContext
        {
            ContentTypeName = context.TypeName,
            ContentFieldSetName = context.FieldSetName,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentTypeFieldSetUpdated(ctx), eventContext, _logger);
    }

    public async Task AlterTypeFieldSetsOrderAsync(ContentTypeDefinition typeDefinition, string[] fieldSetNames)
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync(typeDefinition.Name, type =>
        {
            if (fieldSetNames is null)
            {
                return;
            }

            for (var i = 0; i < fieldSetNames.Length; i++)
            {
                var partDefinition = typeDefinition.FieldSets?.FirstOrDefault(x => x.Name == fieldSetNames[i]);

                if (partDefinition == null)
                {
                    continue;
                }

                type.WithFieldSet(fieldSetNames[i], partDefinition.FieldSetDefinition, part =>
                {
                    part.MergeSettings<ContentTypeFieldSetSettings>(x => x.Position = i.ToString());
                });
            }
        });

        var context = new ContentTypeUpdatedContext
        {
            ContentTypeDefinition = typeDefinition,
        };

        _contentDefinitionEventHandlers.Invoke((handler, context) => handler.ContentTypeUpdated(context), context, _logger);
    }

    public async Task AlterFieldSetFieldsOrderAsync(ContentFieldSetDefinition fieldSetDefinition, string[] fieldNames)
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync(fieldSetDefinition.Name, type =>
        {
            if (fieldNames is null)
            {
                return;
            }

            for (var i = 0; i < fieldNames.Length; i++)
            {
                var fieldDefinition = fieldSetDefinition.Fields.FirstOrDefault(x => x.Name == fieldNames[i]);
                type.WithField(fieldNames[i], field =>
                {
                    field.MergeSettings<ContentFieldSettings>(x => x.Position = i.ToString());
                });
            }
        });

        _contentDefinitionEventHandlers.Invoke((handler, context) => handler.ContentFieldSetUpdated(context), new ContentFieldSetUpdatedContext
        {
            ContentFieldSetDefinition = fieldSetDefinition,
        }, _logger);
    }

    public async Task<string> GenerateContentTypeNameFromDisplayNameAsync(string displayName)
    {
        displayName = displayName.ToSafeName();

        while (await _contentDefinitionManager.LoadTypeDefinitionAsync(displayName) != null)
        {
            displayName = VersionName(displayName);
        }

        return displayName;
    }

    public async Task<string> GenerateFieldNameFromDisplayNameAsync(string fieldSetName, string displayName)
    {
        IEnumerable<ContentFieldDefinition> fieldDefinitions;

        var part = await _contentDefinitionManager.LoadFieldSetDefinitionAsync(fieldSetName);
        displayName = displayName.ToSafeName();

        if (part == null)
        {
            var type = await _contentDefinitionManager.LoadTypeDefinitionAsync(fieldSetName)
                ?? throw new ArgumentException("The part doesn't exist: " + fieldSetName);

            var typeFieldSet = type.FieldSets?.FirstOrDefault(x => x.FieldSetDefinition.Name == fieldSetName);

            // If passed in might be that of a type w/ no implicit field.
            if (typeFieldSet == null)
            {
                return displayName;
            }

            fieldDefinitions = typeFieldSet.FieldSetDefinition.Fields.ToList();
        }
        else
        {
            fieldDefinitions = part.Fields.ToList();
        }

        while (fieldDefinitions.Any(x => string.Equals(displayName.Trim(), x.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            displayName = VersionName(displayName);
        }

        return displayName;
    }

    private static string GetFieldSetPosition(ContentTypeDefinition typeDefinition, string partName)
    {
        var existingFieldSetDefinition = typeDefinition?.FieldSets.FirstOrDefault(part => string.Equals(part.Name, partName, StringComparison.OrdinalIgnoreCase));
        var existingPosition = existingFieldSetDefinition?.GetSettings<ContentTypeFieldSetSettings>().Position;

        if (!string.IsNullOrWhiteSpace(existingPosition))
        {
            return existingPosition;
        }

        return GetNextPosition(typeDefinition?.FieldSets.Select(part => part.GetSettings<ContentTypeFieldSetSettings>().Position));
    }

    private static string GetFieldPosition(ContentFieldSetDefinition partDefinition, string fieldName)
    {
        var existingFieldDefinition = partDefinition?.Fields.FirstOrDefault(field => string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        var existingPosition = existingFieldDefinition?.GetSettings<ContentFieldSettings>().Position;

        if (!string.IsNullOrWhiteSpace(existingPosition))
        {
            return existingPosition;
        }

        return GetNextPosition(partDefinition?.Fields.Select(field => field.GetSettings<ContentFieldSettings>().Position));
    }

    private static string GetNextPosition(IEnumerable<string> positions)
    {
        var nextPosition = 0;
        var currentIndex = 0;

        if (positions != null)
        {
            foreach (var position in positions)
            {
                nextPosition = Math.Max(nextPosition, int.TryParse(position, out var parsedPosition) ? parsedPosition + 1 : currentIndex + 1);
                currentIndex++;
            }
        }

        return nextPosition.ToString();
    }

    private static string VersionName(string name)
    {
        int version;
        var nameParts = name.Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (nameParts.Length > 1 && int.TryParse(nameParts.Last(), out version))
        {
            version = version > 0 ? ++version : 2;

            // This could unintentionally chomp something that looks like a version.
            name = string.Join('-', nameParts.Take(nameParts.Length - 1));
        }
        else
        {
            version = 2;
        }

        return string.Format("{0}-{1}", name, version);
    }
}
