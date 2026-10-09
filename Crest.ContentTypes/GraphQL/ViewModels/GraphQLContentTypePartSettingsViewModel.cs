using Crest.ContentManagement.GraphQL.Options;
using Crest.ContentManagement.GraphQL.Settings;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentTypes.GraphQL.ViewModels;

public class GraphQLContentTypePartSettingsViewModel
{
    public GraphQLContentOptions Options { get; set; }
    public GraphQLContentTypePartSettings Settings { get; set; }
    public ContentTypePartDefinition Definition { get; set; }
}
