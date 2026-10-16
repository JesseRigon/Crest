using System.Collections.Concurrent;
using Grove.Environment.Cache;
using Microsoft.Extensions.Primitives;

namespace Grove.Workflows.Hooks;

/// <summary>
/// A slot's resolved attachments, per tenant: content lifecycle hooks ask at every save, so the
/// resolution (slot sources, the system flows' installed definitions, the tenant's attachments) is
/// kept until something it was read from changes. An entry holds while the slot sources' stamps
/// are unchanged (the content definitions) and no <see cref="SignalKey"/> was raised (a system
/// flow imported, a tenant attachment added or removed, a flow published or removed).
/// </summary>
public sealed class WorkflowHookAttachmentCache(ISignal signal)
{
    public const string SignalKey = "Grove.Workflows.HookAttachments";

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string slotKey, string stamp, out IReadOnlyList<WorkflowHookAttachmentModel> attachments)
    {
        if (_entries.TryGetValue(slotKey, out var entry) && !entry.Token.HasChanged && string.Equals(entry.Stamp, stamp, StringComparison.Ordinal))
        {
            attachments = entry.Attachments;
            return true;
        }

        attachments = [];
        return false;
    }

    /// <summary>The token to read before resolving: an entry is stored under the token current when its resolution started.</summary>
    public IChangeToken Token() => signal.GetToken(SignalKey);

    public void Set(string slotKey, string stamp, IChangeToken token, IReadOnlyList<WorkflowHookAttachmentModel> attachments)
    {
        if (!token.HasChanged)
        {
            _entries[slotKey] = new(stamp, token, attachments);
        }
    }

    private sealed record Entry(string Stamp, IChangeToken Token, IReadOnlyList<WorkflowHookAttachmentModel> Attachments);
}
