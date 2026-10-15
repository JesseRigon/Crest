namespace Grove.Workflows.Common.Multitenancy;

public interface ITenantDeletedEvent
{
    Task TenantDeletedAsync(TenantDeletedEventArgs args);
}