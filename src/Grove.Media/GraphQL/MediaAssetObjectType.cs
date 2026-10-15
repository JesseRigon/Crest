using Grove.Media.Library;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.FileStorage;

namespace Grove.Media.GraphQL;

public class MediaAssetObjectType : ObjectGraphType<IFileStoreEntry>
{
    public MediaAssetObjectType(IStringLocalizer<MediaAssetObjectType> S)
    {
        Name = "MediaAsset";

        Field(file => file.Name).Description(S["The name of the asset."]);

        Field<StringGraphType>("path")
            .Description(S["The url to the asset."])
            .ResolveAsync(async x =>
            {
                // The /files URL of the asset's placement in the media library (ruling F1).
                var references = x.RequestServices.GetRequiredService<IMediaReferenceResolver>();
                return await references.GetUrlAsync(x.Source.Path);
            });

        Field(file => file.Length)
            .Description(S["The length of the file."]);

        Field<DateTimeGraphType>("lastModifiedUtc")
            .Description(S["The date and time in UTC when the asset was last modified."])
            .Resolve(file => file.Source.LastModifiedUtc);
    }
}
