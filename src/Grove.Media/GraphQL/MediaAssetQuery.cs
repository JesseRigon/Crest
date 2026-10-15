using Grove.Media.Library;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Grove.Apis.GraphQL;
using Grove.Apis.GraphQL.Resolvers;
using Grove.ContentManagement.GraphQL.Options;
using Grove.FileStorage;

namespace Grove.Media.GraphQL;

public sealed class MediaAssetQuery : ISchemaBuilder
{
    private readonly GraphQLContentOptions _graphQLContentOptions;

    internal readonly IStringLocalizer S;

    public MediaAssetQuery(
        IStringLocalizer<MediaAssetQuery> localizer,
        IOptions<GraphQLContentOptions> graphQLContentOptions)
    {
        S = localizer;
        _graphQLContentOptions = graphQLContentOptions.Value;
    }

    public Task<string> GetIdentifierAsync() => Task.FromResult(string.Empty);

    public Task BuildAsync(ISchema schema)
    {
        if (_graphQLContentOptions.IsHiddenByDefault("MediaAssets"))
        {
            return Task.CompletedTask;
        }

        var field = new FieldType
        {
            Name = "MediaAssets",
            Description = S["Media assets are items that are part of your media library."],
            Type = typeof(ListGraphType<MediaAssetObjectType>),
            Arguments = new QueryArguments(
                new QueryArgument<StringGraphType>
                {
                    Name = "path",
                    Description = S["Media asset path."],
                },
                new QueryArgument<BooleanGraphType>
                {
                    Name = "includeSubDirectories",
                    Description = S["Whether to get the assets from just the top directory or from all sub-directories as well."],
                }
            ),
            Resolver = new LockedAsyncFieldResolver<IEnumerable<IFileStoreEntry>>(ResolveAsync),
        };

        schema.Query.AddField(field);

        return Task.CompletedTask;
    }

    private async ValueTask<IEnumerable<IFileStoreEntry>> ResolveAsync(IResolveFieldContext resolveContext)
    {
        var mediaFileStore = resolveContext.RequestServices.GetService<IMediaFileStore>();

        var path = resolveContext.GetArgument("path", string.Empty);
        var includeSubDirectories = resolveContext.GetArgument("includeSubDirectories", false);

        var allFiles = mediaFileStore.GetDirectoryContentAsync(path, includeSubDirectories);

        var entries = includeSubDirectories
            ? await allFiles.ToListAsync()
            : await allFiles.Where(x => !x.IsDirectory).ToListAsync();

        // Once the media library is active, only what the caller may view (ruling F1): each entry
        // is a placement, opened through the access check.
        var library = resolveContext.RequestServices.GetService<IMediaLibrary>();
        if (library is null || !await library.IsActiveAsync())
        {
            return entries;
        }

        var references = resolveContext.RequestServices.GetRequiredService<IMediaReferenceResolver>();
        var visible = new List<IFileStoreEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (await references.ResolveAsync(MediaReference.ForPath(entry.Path), MediaUrlOptions.Unsigned) is not null)
            {
                visible.Add(entry);
            }
        }

        return visible;
    }
}
