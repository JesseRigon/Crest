using Grove.Workflows.Extensions;
using Grove.Workflows.Management.Features;
using JetBrains.Annotations;

namespace Grove.Workflows.Management;

[PublicAPI]
public static class WorkflowManagementFeatureExtensions
{
    public static WorkflowManagementFeature AddVariableTypeAndAlias<T>(this WorkflowManagementFeature management, string category)
    {
        return management.AddVariableTypeAndAlias<T>(typeof(T).Name, category);
    }
    
    public static WorkflowManagementFeature AddVariableTypeAndAlias<T>(this WorkflowManagementFeature management, string alias, string category)
    {
        management.AddVariableType<T>(category);
        management.Module.AddTypeAlias<T>(alias);
        return management;
    }
}