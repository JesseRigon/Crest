using Crest.Workflows.Studio.Workflows.UI.Contracts;
using Crest.Workflows.Studio.Workflows.UI.Models;
using MudBlazor;

namespace Crest.Workflows.Designer;

public class ActivityIconProvider : IActivityDisplaySettingsProvider
{
    public IDictionary<string, ActivityDisplaySettings> GetSettings()
    {
        return new Dictionary<string, ActivityDisplaySettings>
        {
            ["Crest.Content.ContentCreated"] = new(PlatformColors.ContentEvent, Icons.Material.Filled.ElectricBolt),
            ["Crest.Content.ContentDraftSaved"] = new(PlatformColors.ContentEvent, Icons.Material.Filled.ElectricBolt),
            ["Crest.Content.ContentUpdated"] = new(PlatformColors.ContentEvent, Icons.Material.Filled.ElectricBolt),
            ["Crest.Content.ContentPublished"] = new(PlatformColors.ContentEvent, Icons.Material.Filled.ElectricBolt),
            ["Crest.Content.ContentUnpublished"] = new(PlatformColors.ContentEvent, Icons.Material.Filled.ElectricBolt),
            ["Crest.Content.ContentVersioned"] = new(PlatformColors.ContentEvent, Icons.Material.Filled.ElectricBolt),
            ["Crest.Content.ContentDeleted"] = new(PlatformColors.ContentEvent, Icons.Material.Filled.ElectricBolt),
            ["Crest.Content.CreateContent"] = new(PlatformColors.ContentAction, PlatformIcons.Heroicons.DocumentPlus),
            ["Crest.Content.UpdateContent"] = new(PlatformColors.ContentAction, PlatformIcons.Tabler.Pen),
            ["Crest.Content.DeleteContent"] = new(PlatformColors.ContentAction, Icons.Material.Filled.Delete),
            ["Crest.Content.GetContent"] = new(PlatformColors.ContentAction, Icons.Material.Filled.FileOpen),
            ["Crest.Content.PublishContent"] = new(PlatformColors.ContentAction, Icons.Material.Filled.CloudUpload),
            ["Crest.Content.UnpublishContent"] = new(PlatformColors.ContentAction, Icons.Material.Filled.CloudDownload),
            ["Crest.Content.ResolveTerm"] = new(PlatformColors.ContentAction, Icons.Material.Filled.ManageSearch),
            ["Crest.UI.DisplayNotification"] = new(PlatformColors.UIAction, Icons.Material.Outlined.Info),
            ["Crest.Queries.RunSqlQuery"] = new(PlatformColors.Queries, PlatformIcons.Tabler.Database),
        };
    }
}