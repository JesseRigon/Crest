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
        /// The Approvals page (blazor-wasm), relative to the admin base: where the Workflows menu
        /// section opens. The pages declare their own routes with <c>@page</c>.
        /// </summary>
        public const string ApprovalsAdmin = "/workflows/approvals";

        /// <summary>The approval queue and decisions.</summary>
        public const string ApprovalsApi = "api/crest/workflows/approvals";

        /// <summary>Hook slots and their attachments.</summary>
        public const string HooksApi = "api/crest/workflows/hooks";

        /// <summary>Inbound webhooks, anonymous and signature-verified: <c>{connection}/{hook}</c>.</summary>
        public const string WebhooksApi = "api/crest/workflows/webhooks";
    }

    /// <summary>
    /// The core objects Crest's own triggers and activities act on, for grouping in the
    /// palette. Object names are free strings: a module names its own records' object.
    /// </summary>
    public static class Objects
    {
        public const string Party = "party";
        public const string Content = "content";

        /// <summary>A platform-wide activity with no core object of its own (e-mail, SMS, social posts, logging).</summary>
        public const string Platform = "platform";

        /// <summary>A tenant user (the Users module's triggers and activities).</summary>
        public const string User = "user";

        /// <summary>A tenant (shell) managed from the default tenant.</summary>
        public const string Tenant = "tenant";

        /// <summary>The workflow system itself (flow-raised triggers).</summary>
        public const string Workflow = "workflow";
    }

    /// <summary>The permission names this feature declares beside the ManageWorkflows umbrella.</summary>
    public static class Permissions
    {
        public const string View = "ViewCrestWorkflows";
        public const string Edit = "EditCrestWorkflows";
        public const string Publish = "PublishCrestWorkflows";
        public const string Run = "RunCrestWorkflows";
        public const string ManageShipped = "ManageShippedCrestWorkflows";
        public const string ManageConnections = "ManageCrestWorkflowConnections";
        /// <summary>Call or poll a connection from a running flow; implied by ManageConnections.</summary>
        public const string UseConnections = "UseCrestWorkflowConnections";
    }

    /// <summary>The roles the recipe ships for the permission set (Administrator and Editor hold the stock umbrella).</summary>
    public static class Roles
    {
        public const string WorkflowEditor = "WorkflowEditor";
        public const string WorkflowViewer = "WorkflowViewer";
    }

    /// <summary>Keys of the workflow input every Registry trigger carries.</summary>
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

    /// <summary>On an engine activity descriptor: the permission the activity's class declares with <see cref="RequiresPermissionAttribute"/>.</summary>
    public const string RequiredPermissionDescriptorProperty = "crest:requiredPermission";

    /// <summary>On an engine activity descriptor: the resource the gate asks
    /// <see cref="RequiredPermissionDescriptorProperty"/> against (a content operation's type
    /// name, so the per-type content permissions decide); absent, the bare permission.</summary>
    public const string PermissionResourceDescriptorProperty = "crest:permissionResource";

    /// <summary>On a content operation descriptor: the content type it acts on.</summary>
    public const string ContentTypeDescriptorProperty = "crest:contentType";

    /// <summary>On a content operation descriptor: the operation (create, update, delete, publish, unpublish).</summary>
    public const string ContentOperationDescriptorProperty = "crest:contentOperation";

    /// <summary>On a run-query activity descriptor: the query it runs (the registry's query kind, docs/operations.md step 3).</summary>
    public const string QueryNameDescriptorProperty = "crest:query";

    /// <summary>On a run-query activity descriptor: the output schema (source, pageable, columns with their types).</summary>
    public const string QuerySchemaDescriptorProperty = "crest:querySchema";

    /// <summary>
    /// Published as system: every burst of the definition runs as the tenant system actor
    /// instead of the actor that started it (timer, cron and delay-started flows must carry
    /// it). Publishing a definition with it set takes ManageShippedWorkflows; it is the only
    /// way a workflow comes to act as the system.
    /// </summary>
    public const string RunsAsSystemProperty = "Crest.RunsAsSystem";

    /// <summary>
    /// Accepts anonymous runs (docs/access.md, Anonymous actors, ruled 2026-10-11): a run with
    /// no signed-in caller is admitted only when the definition carries it and the run starts
    /// at a page-URL HTTP endpoint (a public page). Every other anonymous run is refused with
    /// the reason. Publishing a definition with it set takes ManageShippedWorkflows, like
    /// <see cref="RunsAsSystemProperty"/>; a privileged step in such a flow means publishing it
    /// as system.
    /// </summary>
    public const string AcceptsAnonymousRunsProperty = "Crest.AcceptsAnonymousRuns";

    /// <summary>
    /// A definition's own access lists: role names and user names allowed to edit (save,
    /// publish, retract, delete) or run it. Empty means anyone the permissions allow; the
    /// super user and administrators are never narrowed. Evaluated inside the permission
    /// check as resource-based authorization, not as a separate ACL.
    /// </summary>
    public const string AccessEditProperty = "Crest.Access.Edit";
    public const string AccessRunProperty = "Crest.Access.Run";
    /// <summary>Who may read the definition's instances (list, journal, variables, export); empty = every View holder (ruled 2026-10-13).</summary>
    public const string AccessViewProperty = "Crest.Access.View";
}
