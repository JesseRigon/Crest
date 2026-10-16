using JetBrains.Annotations;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using Grove.Workflows.Indexes;
using Grove.Workflows.FieldSets;
using Grove.Contents.Management;
using YesSql.Sql;
using Grove.Contents.Types;

namespace Grove.Workflows.Migrations;

[UsedImplicitly]
public class WorkflowDefinitionMigrations(IContentDefinitionManager contentDefinitionManager) : DataMigration
{
    private const string Collection = WorkflowCollections.WorkflowDefinitions;
    
    [UsedImplicitly]
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<WorkflowDefinitionIndex>(table => table
            .Column<string>(nameof(WorkflowDefinitionIndex.DefinitionId), c => c.NotNull().WithLength(32))
            .Column<string>(nameof(WorkflowDefinitionIndex.DefinitionVersionId), c => c.NotNull().WithLength(32))
            .Column<string>(nameof(WorkflowDefinitionIndex.Description))
            .Column<int>(nameof(WorkflowDefinitionIndex.Version))
            .Column<bool>(nameof(WorkflowDefinitionIndex.IsLatest))
            .Column<bool>(nameof(WorkflowDefinitionIndex.IsPublished))
            .Column<bool>(nameof(WorkflowDefinitionIndex.IsReadonly))
            .Column<bool>(nameof(WorkflowDefinitionIndex.IsSystem))
            .Column<string>(nameof(WorkflowDefinitionIndex.MaterializerName))
            .Column<bool>(nameof(WorkflowDefinitionIndex.UsableAsActivity))
            .Column<string>(nameof(WorkflowDefinitionIndex.Name), c => c.NotNull())
        , Collection);

        await SchemaBuilder.AlterIndexTableAsync<WorkflowDefinitionIndex>(table =>
        {
            table.CreateIndex("IDX_WorkflowDefinitionIndex_DefinitionId", nameof(WorkflowDefinitionIndex.DefinitionId));
            table.CreateIndex("IDX_WorkflowDefinitionIndex_DefinitionVersionId", nameof(WorkflowDefinitionIndex.DefinitionVersionId));
            table.CreateIndex("IDX_WorkflowDefinitionIndex_UsableAsActivity", nameof(WorkflowDefinitionIndex.UsableAsActivity));
            table.CreateIndex("IDX_WorkflowDefinitionIndex_IsPublished", nameof(WorkflowDefinitionIndex.IsPublished));
            table.CreateIndex("IDX_WorkflowDefinitionIndex_IsLatest", nameof(WorkflowDefinitionIndex.IsLatest));
            table.CreateIndex("IDX_WorkflowDefinitionIndex_Version", nameof(WorkflowDefinitionIndex.Version));
            table.CreateIndex("IDX_WorkflowDefinitionIndex_IsSystem", nameof(WorkflowDefinitionIndex.IsSystem));
            table.CreateIndex("IDX_WorkflowDefinitionIndex_MaterializerName", nameof(WorkflowDefinitionIndex.MaterializerName));
            table.CreateIndex("IDX_WorkflowDefinitionIndex_Name", nameof(WorkflowDefinitionIndex.Name));
            table.CreateIndex("IDX_WorkflowDefinitionIndex_Description", nameof(WorkflowDefinitionIndex.Description));
        }, Collection);

        await contentDefinitionManager.AlterFieldSetDefinitionAsync("WorkflowDefinition", part => part
            .Attachable()
            .WithDisplayName("Workflow Definition")
            .WithDescription("Turns your content item into a workflow definition."));

        await contentDefinitionManager.AlterTypeDefinitionAsync("WorkflowDefinition", type => type
            .WithFieldSet("Common", part => part
                .WithPosition("10")
            )
            .WithFieldSet("WorkflowDefinition")
            .Versionable()
            .Draftable()
            .Creatable()
            .Listable()
            .Securable()
            .WithDescription("A workflow definition is the blueprint for an executable workflow."));

        await DefineWorkflowDefinitionFieldsAsync();

        return 2;
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom1Async()
    {
        await DefineWorkflowDefinitionFieldsAsync();
        return 2;
    }

    private Task DefineWorkflowDefinitionFieldsAsync() =>
        contentDefinitionManager.AlterFieldSetDefinitionAsync("WorkflowDefinition", builder => builder
            .WithField(nameof(WorkflowDefinitionFieldSet.DefinitionId), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("DefinitionId")
                .WithPosition("0")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.DefinitionVersionId), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("DefinitionVersionId")
                .WithPosition("1")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.Version), field => field
                .OfType("NumericField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Version")
                .WithPosition("2")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.Name), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Name")
                .WithPosition("3")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.Description), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Description")
                .WithPosition("4")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.ProviderName), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("ProviderName")
                .WithPosition("5")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.MaterializerName), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("MaterializerName")
                .WithPosition("6")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.UsableAsActivity), field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("UsableAsActivity")
                .WithPosition("7")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.IsSystem), field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("IsSystem")
                .WithPosition("8")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.IsReadonly), field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("IsReadonly")
                .WithPosition("9")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.RunsAsSystem), field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("RunsAsSystem")
                .WithPosition("10")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.AcceptsAnonymousRuns), field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("AcceptsAnonymousRuns")
                .WithPosition("11")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.ToolVersion), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("ToolVersion")
                .WithPosition("12")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField(nameof(WorkflowDefinitionFieldSet.SerializedData), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("SerializedData")
                .WithPosition("13")
                .WithEditor(ContentFieldEditors.None)
                .Locked()));
}