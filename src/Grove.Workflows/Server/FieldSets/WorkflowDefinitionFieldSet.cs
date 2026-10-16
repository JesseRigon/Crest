using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Workflows.FieldSets;

/// <summary>
/// A workflow definition version stored as a content item. Its values are fields (text fields, a
/// numeric field <c>Version</c>, boolean fields for the flags, the serialized definition a text field
/// <c>SerializedData</c>); this class is the typed accessor over them.
/// </summary>
public class WorkflowDefinitionFieldSet : ContentFieldSet
{
    [JsonIgnore]
    public string DefinitionId
    {
        get => this.Get<TextField>(nameof(DefinitionId))?.Text!;
        set => this.Alter<TextField>(nameof(DefinitionId), fieldValue => fieldValue.Text = value);
    }

    [JsonIgnore]
    public string DefinitionVersionId
    {
        get => this.Get<TextField>(nameof(DefinitionVersionId))?.Text!;
        set => this.Alter<TextField>(nameof(DefinitionVersionId), fieldValue => fieldValue.Text = value);
    }

    [JsonIgnore]
    public int Version
    {
        get => this.Get<NumericField>(nameof(Version))?.Value is { } number ? (int)number : 0;
        set => this.Alter<NumericField>(nameof(Version), fieldValue => fieldValue.Value = value);
    }

    [JsonIgnore]
    public string Name
    {
        get => this.Get<TextField>(nameof(Name))?.Text!;
        set => this.Alter<TextField>(nameof(Name), fieldValue => fieldValue.Text = value);
    }

    [JsonIgnore]
    public string? Description
    {
        get => this.Get<TextField>(nameof(Description))?.Text;
        set => this.Alter<TextField>(nameof(Description), fieldValue => fieldValue.Text = value);
    }

    [JsonIgnore]
    public string? ProviderName
    {
        get => this.Get<TextField>(nameof(ProviderName))?.Text;
        set => this.Alter<TextField>(nameof(ProviderName), fieldValue => fieldValue.Text = value);
    }

    [JsonIgnore]
    public string MaterializerName
    {
        get => this.Get<TextField>(nameof(MaterializerName))?.Text!;
        set => this.Alter<TextField>(nameof(MaterializerName), fieldValue => fieldValue.Text = value);
    }

    [JsonIgnore]
    public bool UsableAsActivity
    {
        get => this.Get<BooleanField>(nameof(UsableAsActivity))?.Value ?? false;
        set => this.Alter<BooleanField>(nameof(UsableAsActivity), fieldValue => fieldValue.Value = value);
    }

    [JsonIgnore]
    public bool IsSystem
    {
        get => this.Get<BooleanField>(nameof(IsSystem))?.Value ?? false;
        set => this.Alter<BooleanField>(nameof(IsSystem), fieldValue => fieldValue.Value = value);
    }

    [JsonIgnore]
    public bool IsReadonly
    {
        get => this.Get<BooleanField>(nameof(IsReadonly))?.Value ?? false;
        set => this.Alter<BooleanField>(nameof(IsReadonly), fieldValue => fieldValue.Value = value);
    }

    [JsonIgnore]
    public bool RunsAsSystem
    {
        get => this.Get<BooleanField>(nameof(RunsAsSystem))?.Value ?? false;
        set => this.Alter<BooleanField>(nameof(RunsAsSystem), fieldValue => fieldValue.Value = value);
    }

    [JsonIgnore]
    public bool AcceptsAnonymousRuns
    {
        get => this.Get<BooleanField>(nameof(AcceptsAnonymousRuns))?.Value ?? false;
        set => this.Alter<BooleanField>(nameof(AcceptsAnonymousRuns), fieldValue => fieldValue.Value = value);
    }
    [JsonIgnore]
    public Version? ToolVersion
    {
        get => System.Version.TryParse(this.Get<TextField>(nameof(ToolVersion))?.Text, out var version) ? version : null;
        set => this.Alter<TextField>(nameof(ToolVersion), fieldValue => fieldValue.Text = value?.ToString());
    }

    [JsonIgnore]
    public string SerializedData
    {
        get => this.Get<TextField>(nameof(SerializedData))?.Text!;
        set => this.Alter<TextField>(nameof(SerializedData), fieldValue => fieldValue.Text = value);
    }}
