namespace Crest.Workflows.Common.Multitenancy;

public interface ITenantDeletedEvent
{
    Task TenantDeletedAsync(TenantDeletedEventArgs args);
}