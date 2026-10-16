using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Routing;
using Grove.Routes.Auto.Models;

namespace Grove.Routes.Auto.Projections;

/// <summary>The permalink's route values (a read projection): its path, when the attachment manages contained items' routes.</summary>
public sealed class AutorouteRouteProjection(IContentDefinitionManager contentDefinitionManager) : ContentFieldSetProjection<AutorouteFieldSet>
{
    protected override Task ProjectAsync(ContentItemAspectContext context, AutorouteFieldSet part)
    {
        return context.ForAsync<RouteHandlerAspect>(async aspect =>
        {
            var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(part.ContentItem.ContentType);
            var settings = contentTypeDefinition?.FieldSets
                .FirstOrDefault(x => string.Equals(x.FieldSetDefinition.Name, ContentFieldSetName.Of<AutorouteFieldSet>(), StringComparison.Ordinal))?
                .GetSettings<AutorouteFieldSetSettings>();
            if (settings?.ManageContainedItemRoutes == true)
            {
                aspect.Path = part.Path;
                aspect.Absolute = part.Absolute;
                aspect.Disabled = part.Disabled;
            }
        });
    }
}
