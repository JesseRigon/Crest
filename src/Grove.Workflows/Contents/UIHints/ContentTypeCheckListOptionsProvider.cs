using System.Reflection;
using Grove.Workflows.UIHints.CheckList;
using Open.Linq.AsyncExtensions;
using Grove.Contents.Management.Metadata;

namespace Grove.Workflows.Contents.UIHints;

public class ContentTypeCheckListOptionsProvider(IContentDefinitionManager contentDefinitionManager) : CheckListOptionsProviderBase
{
    protected override async ValueTask<ICollection<CheckListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken)
    {
        var creatableContentTypeDefinitions = await contentDefinitionManager.ListTypeDefinitionsAsync().ToList();
        return creatableContentTypeDefinitions.Select(x => new CheckListItem(x.DisplayName, x.Name)).ToList();
    }
}