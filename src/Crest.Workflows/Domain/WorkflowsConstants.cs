namespace Crest.Workflows;

public static class WorkflowsConstants
{
    public const string FeatureId = "Crest.Workflows";
    public const string ContentsFeatureId = "Crest.Workflows.Contents";

    public static class Routes
    {
        /// <summary>The engine's own HTTP API, mapped in the tenant (definitions, instances, journal).</summary>
        public const string EngineApi = "crest-workflows/api";

        /// <summary>The Crest-style registry API: what modules registered, and where it landed.</summary>
        public const string RegistryApi = "api/crest/workflows/registry";
        public const string TriggersApi = "api/crest/workflows/triggers";
        public const string ConnectionsApi = "api/crest/workflows/connections";
        /// <summary>Under connections: start the OAuth2 authorization-code dance for a connection.</summary>
        public const string ConnectionOAuthAuthorizeApi = "{key}/oauth/authorize";
        /// <summary>Under connections: where the provider sends the user back with the code.</summary>
        public const string ConnectionOAuthCallbackApi = "oauth/callback";
        /// <summary>Under connections: create a connection from an OpenAPI document.</summary>
        public const string ConnectionOpenApiImportApi = "import-openapi";
        /// <summary>Under the registry: <c>flows/{key}/reset</c> re-imports a shipped flow.</summary>
        public const string RegistryFlowResetApi = "flows/{key}/reset";
        /// <summary>A definition's access lists: <c>{definitionId}/access</c>.</summary>
        public const string DefinitionsApi = "api/crest/workflows/definitions";
        public const string DefinitionAccessApi = "{definitionId}/access";
        /// <summary>A definition's field dependencies resolved against the tenant's definitions: <c>{definitionId}/field-dependencies</c>.</summary>
        public const string DefinitionFieldDependenciesApi = "{definitionId}/field-dependencies";
        /// <summary>What is still pending for an object: running or suspended flows about it and their background jobs.</summary>
        public const string PendingApi = "api/crest/workflows/pending";
        /// <summary>Under the registry: <c>sync</c> re-runs the shipped-flow import (installs, upgrades unforked copies on a version bump, re-publishes system flows).</summary>
        public const string RegistrySyncApi = "sync";

        /// <summary>
        /// The admin pages (blazor-wasm), relative to the admin base. They are Studio's own
        /// routes, so the forked Studio's navigation between them lands on them.
        /// </summary>
        public const string DefinitionsAdmin = "/workflows/definitions";
        public const string DefinitionEditAdmin = "/workflows/definitions/{definitionId}/edit";
        public const string InstancesAdmin = "/workflows/instances";
        public const string InstanceViewAdmin = "/workflows/instances/{id}/view";
        public const string ConnectionsAdmin = "/workflows/connections";
        public const string ApprovalsAdmin = "/workflows/approvals";

        /// <summary>The approval queue and decisions.</summary>
        public const string ApprovalsApi = "api/crest/workflows/approvals";

        /// <summary>Hook slots and their attachments.</summary>
        public const string HooksApi = "api/crest/workflows/hooks";

        /// <summary>Inbound webhooks, anonymous and signature-verified: <c>{connection}/{hook}</c>.</summary>
        public const string WebhooksApi = "api/crest/workflows/webhooks";
    }

    /// <summary>The core object a trigger or activity acts on, for grouping in the palette.</summary>
    public static class Objects
    {
        public const string Party = "party";
        public const string Account = "account";
        public const string Asset = "asset";
        public const string Transaction = "transaction";
        public const string Content = "content";

        /// <summary>A stock platform activity (users, e-mail, notifications, tenants, ...).</summary>
        public const string Platform = "platform";

        /// <summary>The workflow system itself (flow-raised triggers).</summary>
        public const string Workflow = "workflow";
    }

    /// <summary>The permission names this feature declares (ManageWorkflows is the stock module's).</summary>
    public static class Permissions
    {
        public const string View = "ViewCrestWorkflows";
        public const string Edit = "EditCrestWorkflows";
        public const string Publish = "PublishCrestWorkflows";
        public const string Run = "RunCrestWorkflows";
        public const string ManageShipped = "ManageShippedCrestWorkflows";
        public const string ManageConnections = "ManageCrestWorkflowConnections";
    }

    /// <summary>The roles the recipe ships for the permission set (Administrator and Editor hold the stock umbrella).</summary>
    public static class Roles
    {
        public const string WorkflowEditor = "WorkflowEditor";
        public const string WorkflowViewer = "WorkflowViewer";
    }

    /// <summary>Keys of the workflow input every Crest trigger carries.</summary>
    public static class InputKeys
    {
        public const string TriggerKey = "TriggerKey";
        public const string Payload = "Payload";
        // Not "User": stock Users events already carry the Crest user under that key.
        public const string Actor = "Actor";
        /// <summary>On a hook attachment's run: the slot it was run for.</summary>
        public const string HookSlot = "HookSlot";
        /// <summary>A unique id per raised stimulus (the idempotency key of the event side): a consumer that must act once per event keys on it.</summary>
        public const string StimulusId = "StimulusId";
    }

    /// <summary>The custom property a shipped definition carries so the importer can find it again.</summary>
    public const string FlowKeyProperty = "Crest.FlowKey";

    /// <summary>The ownership tier (<c>WorkflowOwnership</c>); absent means the tenant's own.</summary>
    public const string OwnershipProperty = "Crest.Ownership";

    /// <summary>The shipped version a system or shipped definition was imported from.</summary>
    public const string FlowVersionProperty = "Crest.FlowVersion";

    /// <summary>Set on a shipped definition the first time a tenant user saves it; cleared by a reset.</summary>
    public const string ForkedProperty = "Crest.Forked";

    /// <summary>
    /// Stamped on a definition version at publish: the field-dependency warnings found against
    /// the tenant's content definitions at that moment (a list of
    /// <c>WorkflowFieldDependencyWarning</c>; absent when there were none). Publish never
    /// refuses for these; the warning is the reminder.
    /// </summary>
    public const string FieldWarningsProperty = "Crest.FieldWarnings";

    /// <summary>On an engine activity descriptor: the field dependencies the activity's class declares.</summary>
    public const string FieldDependenciesDescriptorProperty = "crest:fieldDependencies";

    /// <summary>
    /// A definition's own access lists: role names and user names allowed to edit (save,
    /// publish, retract, delete) or run it. Empty means anyone the permissions allow; the
    /// super user and administrators are never narrowed. Evaluated inside the permission
    /// check as resource-based authorization, not as a separate ACL.
    /// </summary>
    public const string AccessEditProperty = "Crest.Access.Edit";
    public const string AccessRunProperty = "Crest.Access.Run";
}
