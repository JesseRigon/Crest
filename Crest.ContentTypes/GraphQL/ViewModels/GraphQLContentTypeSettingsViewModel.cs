using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.ContentManagement.GraphQL.Options;
using Crest.ContentManagement.GraphQL.Settings;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.ContentTypes.GraphQL.ViewModels;

public class GraphQLContentTypeSettingsViewModel
{
    public GraphQLContentTypeSettings Settings { get; set; }

    [BindNever]
    public GraphQLContentOptions Options { get; set; }

    [BindNever]
    public ContentTypeDefinition Definition { get; set; }
}
