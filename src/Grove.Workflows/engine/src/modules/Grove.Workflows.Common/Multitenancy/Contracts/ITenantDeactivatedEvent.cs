namespace Grove.Workflows.Common.Multitenancy;

public interface ITenantDeactivatedEvent
{
    Task TenantDeactivatedAsync(TenantDeactivatedEventArgs args);
}