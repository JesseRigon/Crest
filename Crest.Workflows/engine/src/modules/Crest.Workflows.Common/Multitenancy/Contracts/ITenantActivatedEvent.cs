namespace Crest.Workflows.Common.Multitenancy;

public interface ITenantActivatedEvent
{
    Task TenantActivatedAsync(TenantActivatedEventArgs args);
}