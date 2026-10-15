using System.Reflection;
using Grove.Workflows.UIHints.Dropdown;
using Open.Linq.AsyncExtensions;
using Grove.Contents.Management.Metadata;

namespace Grove.Workflows.Contents.UIHints;

public class ContentTypeOptionsProvider(IContentDefinitionManager contentDefinitionManager) : DropDownOptionsProviderBase
{
    protected override async ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken)
    {
        var creatableContentTypeDefinitions = await contentDefinitionManager.ListTypeDefinitionsAsync().ToList();
        return creatableContentTypeDefinitions.Select(x => new SelectListItem(x.DisplayName, x.Name)).ToList();
    }
}