using System.Text.Encodings.Web;
using Cysharp.Text;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Grove.Data.Protocols.GraphQL;
using Grove.Contents.Management.Display;
using Grove.Contents.Management.GraphQL.Options;
using Grove.Display.Management;

namespace Grove.Contents.Management.GraphQL.Queries.Types;

public sealed class ContentItemType : ObjectGraphType<ContentItem>
{
    private readonly GraphQLContentOptions _options;

    public ContentItemType(IOptions<GraphQLContentOptions> optionsAccessor, IStringLocalizer<ContentItemType> S)
    {
        _options = optionsAccessor.Value;

        Name = "ContentItemType";

        Field(ci => ci.ContentItemId)
            .Description(S["Content item id"]);

        Field(ci => ci.ContentItemVersionId, nullable: true)
            .Description(S["The content item version id"]);

        Field(ci => ci.ContentType)
            .Description(S["Type of content"]);

        Field(ci => ci.DisplayText, nullable: true)
            .Description(S["The display text of the content item"]);

        Field(ci => ci.Published)
            .Description(S["Is the published version"]);

        Field(ci => ci.Latest)
            .Description(S["Is the latest version"]);

        Field<DateTimeGraphType>("modifiedUtc")
            .Resolve(ci => ci.Source.ModifiedUtc)
            .Description(S["The date and time of modification"]);

        Field<DateTimeGraphType>("publishedUtc")
            .Resolve(ci => ci.Source.PublishedUtc)
            .Description(S["The date and time of publication"]);

        Field<DateTimeGraphType>("createdUtc")
            .Resolve(ci => ci.Source.CreatedUtc)
            .Description(S["The date and time of creation"]);

        Field(ci => ci.Owner, nullable: true)
            .Description(S["The owner of the content item"]);

        Field(ci => ci.Author)
            .Description(S["The author of the content item"]);

        Field<StringGraphType, string>("render")
            .ResolveLockedAsync(RenderShapeAsync);

        Interface<ContentItemInterface>();

        IsTypeOf = IsContentType;
    }

    private bool IsContentType(object obj)
    {
        return obj is ContentItem item && item.ContentType == Name;
    }

    public override FieldType AddField(FieldType fieldType)
    {
        if (!_options.ShouldSkip(GetType(), fieldType.Name))
        {
            return base.AddField(fieldType);
        }

        return null;
    }

    private static async ValueTask<string> RenderShapeAsync(IResolveFieldContext<ContentItem> context)
    {
        var serviceProvider = context.RequestServices;

        // Build shape
        var displayManager = serviceProvider.GetRequiredService<IContentItemDisplayManager>();
        var model = await displayManager.BuildDisplayAsync(context.Source);

        // The display tree, filtered for the caller and rendered to static HTML (the display's HTML path).
        var tree = await serviceProvider.GetRequiredService<Grove.Display.Management.Components.IDisplayTreeBuilder>().BuildAsync(model, cancellationToken: context.CancellationToken);
        var caller = serviceProvider.GetService<Grove.Access.ICallerContextAccessor>()?.Current;
        var root = await serviceProvider.GetRequiredService<Grove.Display.Management.Components.IDisplayTreeFilter>().FilterAsync(tree.Root, caller, context.CancellationToken);

        return root is null
            ? string.Empty
            : await serviceProvider.GetRequiredService<Grove.Display.Management.Components.IDisplayHtmlRenderer>().RenderAsync(root, context.CancellationToken);
    }

    public override void Initialize(ISchema schema)
    {
        if (schema is IServiceProvider serviceProvider)
        {
            var initializers = serviceProvider.GetServices<IContentItemTypeInitializer>();

            foreach (var initializer in initializers)
            {
                initializer.Initialize(this, schema);
            }
        }

        base.Initialize(schema);
    }
}
