using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;

namespace Grove.Workflows.Contexts;

/// <summary>
/// The actor as it travels in a workflow input: sealed with the tenant's data protection, so
/// only the server can produce one and nothing a client, an author's expression or an import
/// supplies is read as an actor. A capture site seals a <em>fresh</em> actor (valid for a day,
/// for a run not yet created); the gate binds it to the instance on the run's first burst, and
/// every later burst opens only an actor bound to its own instance, so a sealed actor copied
/// from one run cannot be replayed into another.
/// </summary>
public sealed class WorkflowActorSeal(IDataProtectionProvider dataProtection)
{
    private static readonly TimeSpan FreshLifetime = TimeSpan.FromDays(1);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ITimeLimitedDataProtector _fresh = dataProtection.CreateProtector("Workflows.Actor.Fresh").ToTimeLimitedDataProtector();
    private readonly IDataProtector _bound = dataProtection.CreateProtector("Workflows.Actor.Bound");

    /// <summary>An actor for a run that does not exist yet: a trigger, a dispatch, a hook.</summary>
    public string Seal(WorkflowUserContext actor) =>
        _fresh.Protect(JsonSerializer.Serialize(actor, Json), FreshLifetime);

    /// <summary>The run's actor, bound to its instance.</summary>
    public string Bind(WorkflowUserContext actor, string instanceId) =>
        _bound.Protect(JsonSerializer.Serialize(new BoundActor(instanceId, actor), Json));

    /// <summary>
    /// The actor in <paramref name="value"/>, or null when it is not one the server sealed:
    /// one bound to <paramref name="instanceId"/>, or a fresh one when
    /// <paramref name="acceptFresh"/> (the run's first burst).
    /// </summary>
    public WorkflowUserContext? Open(object? value, string? instanceId, bool acceptFresh)
    {
        if (value is not string sealedActor || sealedActor.Length == 0)
        {
            return null;
        }

        try
        {
            var bound = JsonSerializer.Deserialize<BoundActor>(_bound.Unprotect(sealedActor), Json);
            return bound is not null && instanceId is not null && string.Equals(bound.InstanceId, instanceId, StringComparison.Ordinal) ? bound.Actor : null;
        }
        catch (CryptographicException)
        {
            // Not a bound actor; it may be a fresh one.
        }

        if (!acceptFresh)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WorkflowUserContext>(_fresh.Unprotect(sealedActor), Json);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private sealed record BoundActor(string InstanceId, WorkflowUserContext Actor);
}
