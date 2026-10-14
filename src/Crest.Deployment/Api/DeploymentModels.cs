#nullable enable

using System.Text.Json.Nodes;

namespace Crest.Deployment.Api;

// The Deployments pages' wire shapes (the admin client's Crest.AdminTheme.Api records).

public sealed record DeploymentPlanSummary(long Id, string Name, int StepCount);

public sealed record DeploymentPlanName(string Name);

public sealed record DeploymentPlanIds(long[]? Ids);

public sealed record DeploymentStepModel(string Id, string Type, string? Category, JsonObject Properties);

public sealed record DeploymentStepType(string Type, string? Category, JsonObject Defaults);

public sealed record DeploymentStepWrite(string Type, JsonObject? Properties);

public sealed record DeploymentStepMove(int OldIndex, int NewIndex);

public sealed record DeploymentTargetRef(string Id, string Name);

public sealed record DeploymentPlanDetail(long Id, string Name, DeploymentStepModel[] Steps, DeploymentStepType[] StepTypes, DeploymentTargetRef[] RemoteInstances, bool CanExport);

public sealed record DeploymentResult(bool Succeeded, string? Message);

public sealed record DeploymentJsonImport(string Json);
